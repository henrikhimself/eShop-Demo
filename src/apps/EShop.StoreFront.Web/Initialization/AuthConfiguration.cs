// <copyright file="AuthConfiguration.cs" company="Henrik Jensen">
// Copyright 2026 Henrik Jensen
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Security.Claims;
using EPiServer.Security;
using Hj.EShop.Common;
using Hj.EShop.ServiceDefaults;
using Hj.EShop.TicketStore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Hj.EShop.StoreFront.Web.Initialization;

internal static class AuthConfiguration
{
    public static void AddAuthConfiguration(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        services.Configure<ClaimTypeOptions>(options =>
        {
            options.Email = "email";
            options.GivenName = "given_name";
            options.Surname = "family_name";
        });

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(
                CookieAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    options.LoginPath = "/util/Login";
                    options.ExpireTimeSpan = TimeSpan.FromHours(1);
                    options.SlidingExpiration = true;

                    // Mirrors EShop.SellerPortal.Bff's OidcSignOutTokenRefresh: always
                    // re-validates against Keycloak on every request, since a Keycloak
                    // restart can forget a session without advancing the saved id_token's
                    // exp claim (doc/CHRONICLE.md). Skipped for Entra External ID, whose
                    // production URLs are assumed stable.
                    options.Events.OnValidatePrincipal = async context =>
                    {
                        OpenIdConnectOptions? oidcOptions = context.HttpContext.RequestServices
                            .GetService<IOptionsMonitor<OpenIdConnectOptions>>()
                            ?.Get(OpenIdConnectDefaults.AuthenticationScheme);
                        OpenIdConnectConfiguration? oidcConfiguration = oidcOptions?.ConfigurationManager is null
                            ? null
                            : await oidcOptions.ConfigurationManager.GetConfigurationAsync(context.HttpContext.RequestAborted);

                        string? identityProvider = configuration["Identity:Provider"];
                        bool alwaysConfirmWithProvider = identityProvider != KnownNames.IdentityProviderEntraExternalId;

                        RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
                            context.Properties.GetTokenValue("id_token"),
                            context.Properties.GetTokenValue("refresh_token"),
                            context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(),
                            oidcConfiguration is null ? null : new Uri(oidcConfiguration.TokenEndpoint),
                            oidcOptions?.ClientId ?? string.Empty,
                            oidcOptions?.ClientSecret,
                            alwaysConfirmWithProvider,
                            TimeProvider.System,
                            context.HttpContext.RequestAborted);

                        if (outcome.Kind == RefreshOutcomeKind.Refreshed)
                        {
                            context.Properties.UpdateTokenValue("id_token", outcome.Tokens!.IdToken);
                            if (outcome.Tokens.RefreshToken is not null)
                            {
                                // Keycloak rotates refresh tokens on every use, so the
                                // next refresh attempt needs this newly issued one.
                                context.Properties.UpdateTokenValue("refresh_token", outcome.Tokens.RefreshToken);
                            }

                            context.ShouldRenew = true;
                        }
                        else if (outcome.Kind == RefreshOutcomeKind.Rejected)
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                    };
                })
            .AddOpenIdConnect(options =>
            {
                string? identityProvider = configuration["Identity:Provider"];
                switch (identityProvider)
                {
                    case KnownNames.IdentityProviderEntraExternalId:
                        ConfigureEntraIdOidc(options, configuration);
                        break;

                    default: // KnownNames.IdentityProviderKeycloak, or absent (test host)
                        ConfigureKeycloakOidc(options, configuration, webHostEnvironment);
                        break;
                }

                ConfigureCommonOidc(options, webHostEnvironment);
            });

        // Used only by OnValidatePrincipal above, for the refresh-token grant call; its
        // handler is configured centrally in EShop.ServiceDefaults (non-Production only).
        services.AddHttpClient();

        // Skipped when using fake infrastructure - no real IConnectionMultiplexer to bind
        // the key ring's XmlRepository to.
        if (webHostEnvironment.ShouldUseRealInfrastructure())
        {
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(configuration.GetConnectionString(KnownNames.ResourceCache)!));
            services.AddStackExchangeRedisCache(
                options => options.Configuration = configuration.GetConnectionString(KnownNames.ResourceCache));

            services.AddOptions<KeyManagementOptions>()
                .Configure<IConnectionMultiplexer>((options, redis) =>
                    options.XmlRepository = new RedisXmlRepository(() => redis.GetDatabase(), "DataProtection-Keys"));
        }

        // SetApplicationName isolates this app's Data Protection keys if another app
        // later shares the same Redis instance; the actual XmlRepository is set above,
        // inside the infrastructure guard.
        services.AddDataProtection()
            .SetApplicationName("EShop.Storefront");

        // The "storefront-ticket:" prefix avoids colliding with EShop.SellerPortal.Bff's
        // tickets in the same shared Redis "cache" resource.
        services.AddSingleton<ITicketStore>(
            sp => new HybridCacheTicketStore(sp.GetRequiredService<HybridCache>(), "storefront-ticket:"));
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<ITicketStore>((options, store) => options.SessionStore = store);
    }

    private static void ConfigureEntraIdOidc(OpenIdConnectOptions options, IConfiguration configuration)
    {
        string entraTenantSubdomain = configuration["Identity:Entra:TenantSubdomain"]
            ?? throw new InvalidOperationException("The 'Identity:Entra:TenantSubdomain' configuration value is missing.");
        options.Authority = $"https://{entraTenantSubdomain}.ciamlogin.com/";
        options.ClientId = configuration["Identity:Entra:ClientId"]
            ?? throw new InvalidOperationException("The 'Identity:Entra:ClientId' configuration value is missing.");
        options.ClientSecret = configuration["Identity:Entra:ClientSecret"];

        // Entra's App Roles surface as a "roles" JSON array claim; MapJsonKey expands it
        // into one Role claim per element, same as Keycloak's "role" claim below, so
        // RequireRole(...) works regardless of provider.
        options.ClaimActions.MapJsonKey(ClaimTypes.Role, "roles");
    }

    private static void ConfigureKeycloakOidc(OpenIdConnectOptions options, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        // Uses the stable reverse-proxy identity host, not the internal Keycloak
        // discovery endpoint - Keycloak's own KC_HOSTNAME is set to the same host in
        // AppHost.cs, so discovery/issuer/redirects all agree.
        options.Authority = KnownValues.KeycloakAuthority;
        options.ClientId = KnownNames.StorefrontOidcClientId;
        options.ClientSecret = configuration["Keycloak:ClientSecret"];

        // Surfaces every realm role as a ClaimTypes.Role claim via the Storefront
        // client's role mapper, statically registered in eshop-realm.json.
        options.ClaimActions.MapJsonKey(ClaimTypes.Role, "role");

        // The reverse proxy's self-signed dev CA is not trusted by Storefront's own
        // backchannel HttpClient (metadata discovery, token exchange) - only a
        // developer's OS/browser trusts it (see DEVELOP.md). Non-Production only; never
        // for Entra.
        if (!webHostEnvironment.IsProduction())
        {
            options.BackchannelHttpHandler = TestingDefaults.CreateLenientHttpHandler();
        }
    }

    private static void ConfigureCommonOidc(OpenIdConnectOptions options, IWebHostEnvironment webHostEnvironment)
    {
        options.RequireHttpsMetadata = webHostEnvironment.IsProduction();

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;

        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.CallbackPath = "/signin-oidc";
        options.SignedOutCallbackPath = "/signout-callback-oidc";

        options.Scope.Clear();
        options.Scope.Add(OpenIdConnectScope.OpenId);
        options.Scope.Add(OpenIdConnectScope.Profile);
        options.Scope.Add(OpenIdConnectScope.Email);
        options.GetClaimsFromUserInfoEndpoint = true;

        options.ClaimActions.MapUniqueJsonKey("preferred_username", "preferred_username");
        options.ClaimActions.MapUniqueJsonKey("email", "email");
        options.ClaimActions.MapUniqueJsonKey("given_name", "given_name");
        options.ClaimActions.MapUniqueJsonKey("family_name", "family_name");

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };

        options.Events.OnTicketReceived = async context =>
        {
            if (context.Principal?.Identity is not ClaimsIdentity identity)
            {
                throw new InvalidOperationException("Keycloak did not produce an authenticated ClaimsIdentity.");
            }

            ValidateRequiredClaims(identity);

            await context.HttpContext.RequestServices
                .GetRequiredService<ISynchronizingUserService>()
                .SynchronizeAsync(identity);

            if (context.Properties is { } properties)
            {
                OidcTokenPruning.StripUnusedTokens(properties);
            }
        };
    }

    internal static void ValidateRequiredClaims(ClaimsIdentity identity)
    {
        string[] requiredClaimTypes = ["preferred_username", "email", "given_name", "family_name"];
        foreach (string claimType in requiredClaimTypes)
        {
            string? claimValue = identity.FindFirst(claimType)?.Value;
            if (string.IsNullOrWhiteSpace(claimValue))
            {
                throw new InvalidOperationException($"The OpenID Connect ticket is missing a required '{claimType}' claim.");
            }
        }
    }
}
