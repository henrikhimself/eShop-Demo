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
using StackExchange.Redis;

namespace Hj.EShop.SellerPortal.Bff.Authentication;

internal static class AuthConfiguration
{
    public static void AddAuthConfiguration(this WebApplicationBuilder builder)
    {
        // See doc/CHRONICLE.md ("Building the OpenAPI-as-frontend-type-source pipeline")
        // - only AddOpenIdConnect needs guarding during build-time doc generation.
        AuthenticationBuilder authenticationBuilder = builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                // Not "__Host-"-prefixed: that mandates Secure (HTTPS-only), which would
                // break local HTTP dev. SameAsRequest still sends Secure over HTTPS.
                options.Cookie.Name = "seller-portal";
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                // Safe: the browser only ever sees one origin, via the Next.js proxy.
                options.Cookie.SameSite = SameSiteMode.Lax;

                // Every endpoint here is an API, not a page: an unauthenticated call
                // gets a plain 401/403, never a login-page redirect.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };

                // See doc/CHRONICLE.md ("System invariants and constraints", the
                // id_token_hint entries) - this is the one place that must catch a stale
                // id_token, including for logout.
                options.Events.OnValidatePrincipal = async context =>
                {
                    OpenIdConnectOptions? oidcOptions = context.HttpContext.RequestServices
                        .GetService<IOptionsMonitor<OpenIdConnectOptions>>()
                        ?.Get(OpenIdConnectDefaults.AuthenticationScheme);
                    OpenIdConnectConfiguration? configuration = oidcOptions?.ConfigurationManager is null
                        ? null
                        : await oidcOptions.ConfigurationManager.GetConfigurationAsync(context.HttpContext.RequestAborted);

                    // Always confirmed against the provider here (not just at sign-out)
                    // for Keycloak only - see doc/CHRONICLE.md. Entra's URLs are assumed
                    // stable, so it's skipped there.
                    string? identityProvider = builder.Configuration["Identity:Provider"];
                    bool alwaysConfirmWithProvider = identityProvider != KnownNames.IdentityProviderEntraExternalId;

                    RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
                        context.Properties.GetTokenValue("id_token"),
                        context.Properties.GetTokenValue("refresh_token"),
                        context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(),
                        configuration is null ? null : new Uri(configuration.TokenEndpoint),
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
                            // Keycloak rotates refresh tokens on every use - the old one
                            // is already spent, so the next refresh attempt needs this
                            // one.
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
            });

        if (!EnvironmentChecks.IsBuildTimeOpenApiGeneration())
        {
            // Used only by AddCookie's OnValidatePrincipal above for the refresh-token
            // grant call - via IHttpClientFactory, not OpenIdConnectOptions.Backchannel,
            // so its handler is configured centrally in EShop.ServiceDefaults.
            builder.Services.AddHttpClient();

            authenticationBuilder.AddOpenIdConnect(options =>
            {
                // Set by AppHost.cs (ADR 0002/0010/0020). Falls through to Keycloak, not
                // a throw, since the test host never sets this.
                string? identityProvider = builder.Configuration["Identity:Provider"];

                switch (identityProvider)
                {
                    case KnownNames.IdentityProviderEntraExternalId:
                        ConfigureEntraIdOidc(options, builder.Configuration);
                        break;

                    default: // KnownNames.IdentityProviderKeycloak, or absent (test host)
                        ConfigureKeycloakOidc(options, builder.Configuration, builder.Environment);
                        break;
                }

                ConfigureCommonOidc(options, builder.Environment);
            });
        }

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("SellerOnly", policy => policy.RequireRole("Seller"));

        // See doc/CHRONICLE.md ("Deferring a DI-resolved dependency..."). Skipped in
        // "Fake" and during build-time OpenAPI generation, matching Program.cs's guard.
        if (builder.Environment.ShouldUseRealInfrastructure())
        {
            builder.Services.AddOptions<KeyManagementOptions>()
                .Configure<IConnectionMultiplexer>((options, redis) =>
                    options.XmlRepository = new RedisXmlRepository(() => redis.GetDatabase(), "DataProtection-Keys"));
        }

        // SetApplicationName guards against key material being shared if another app
        // pointed at the same Redis instance. The XmlRepository is set above.
        builder.Services.AddDataProtection()
            .SetApplicationName("EShop.SellerPortal");

        // HybridCache is registered in Program.cs. The key prefix keeps this app's
        // tickets from colliding with EShop.StoreFront.Web's in the shared Redis
        // instance.
        builder.Services.AddSingleton<ITicketStore>(sp =>
            new HybridCacheTicketStore(sp.GetRequiredService<HybridCache>(), "seller-portal-ticket:"));
        builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<ITicketStore>((options, store) => options.SessionStore = store);
    }

    private static void ConfigureEntraIdOidc(OpenIdConnectOptions options, ConfigurationManager configuration)
    {
        string entraTenantSubdomain = configuration["Identity:Entra:TenantSubdomain"]
            ?? throw new InvalidOperationException("The 'Identity:Entra:TenantSubdomain' configuration value is missing.");
        options.Authority = $"https://{entraTenantSubdomain}.ciamlogin.com/";
        options.ClientId = configuration["Identity:Entra:ClientId"]
            ?? throw new InvalidOperationException("The 'Identity:Entra:ClientId' configuration value is missing.");
        options.ClientSecret = configuration["Identity:Entra:ClientSecret"];

        // Entra's App Roles surface as a "roles" claim (a JSON array); MapJsonKey adds
        // one Role claim per element, matching Keycloak's mapping below.
        options.ClaimActions.MapJsonKey(ClaimTypes.Role, "roles");
    }

    private static void ConfigureKeycloakOidc(OpenIdConnectOptions options, ConfigurationManager configuration, IHostEnvironment environment)
    {
        // The stable reverse-proxy identity host (ADR 0025), not the internal
        // "services:keycloak:https:0" discovery endpoint. Keycloak's own KC_HOSTNAME is
        // set to the same host in AppHost.cs, so discovery/issuer/redirects all agree.
        options.Authority = KnownValues.KeycloakAuthority;
        options.ClientId = KnownNames.SellerPortalOidcClientId;
        options.ClientSecret = configuration["Keycloak:ClientSecret"];

        // Surfaces the "Seller" realm role as a standard ClaimTypes.Role claim so the
        // "SellerOnly" policy's RequireRole works.
        options.ClaimActions.MapJsonKey(ClaimTypes.Role, "role");

        // Non-Production only - both native and containerized dev launches run this
        // resource in the Development environment (see TestingDefaults); never Entra.
        if (!environment.IsProduction())
        {
            options.BackchannelHttpHandler = TestingDefaults.CreateLenientHttpHandler();
        }
    }

    private static void ConfigureCommonOidc(OpenIdConnectOptions options, IHostEnvironment environment)
    {
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.CallbackPath = "/bff/signin-oidc";
        options.SignedOutCallbackPath = "/bff/signout-callback-oidc";
        options.SaveTokens = true;
        options.RequireHttpsMetadata = environment.IsProduction();

        options.Events.OnTicketReceived = context =>
        {
            if (context.Properties is { } properties)
            {
                OidcTokenPruning.StripUnusedTokens(properties);
            }

            return Task.CompletedTask;
        };
    }
}
