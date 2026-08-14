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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using StackExchange.Redis;

namespace Hj.EShop.SellerPortal.Bff.Authentication;

internal static class AuthConfiguration
{
    public static void AddAuthConfiguration(this WebApplicationBuilder builder, bool isBuildTimeOpenApiGeneration)
    {
        // Only AddOpenIdConnect is skipped during build-time OpenAPI generation - its
        // configure delegate reads a Keycloak service-discovery key only present under
        // `aspire start`. AddCookie needs no external config, so a scheme stays
        // registered regardless.
        AuthenticationBuilder authenticationBuilder = builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                // Not "__Host-"-prefixed: that prefix mandates Secure (HTTPS-only),
                // which would silently break local dev - the Next.js proxy and BFF talk
                // plain HTTP there. SameAsRequest (the default, set explicitly here)
                // still sends Secure whenever the request itself is HTTPS, e.g. in a
                // real deployment.
                options.Cookie.Name = "seller-portal";
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                // Safe: the browser only ever sees one origin, via the Next.js reverse
                // proxy.
                options.Cookie.SameSite = SameSiteMode.Lax;

                // Every endpoint here is an API, not a page - an unauthenticated call
                // gets a plain 401/403, not a redirect to a login page that doesn't
                // exist.
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

                // The only place that needs to catch a stale id_token, including for
                // logout: OpenIdConnectHandler.HandleSignOutAsync reads id_token via
                // Context.GetTokenAsync(SignOutScheme, "id_token"), which reuses this
                // same request's already-validated (or already-rejected) authenticate
                // result, since UseAuthentication() runs this handler once per request
                // regardless of the endpoint. A Seller whose refresh also fails (the
                // identity provider has fully forgotten the session, not just expired a
                // token) is signed out here rather than continuing on a session only
                // the Bff still believes in - see OidcSignOutTokenRefresh.
                options.Events.OnValidatePrincipal = async context =>
                {
                    OpenIdConnectOptions? oidcOptions = context.HttpContext.RequestServices
                        .GetService<IOptionsMonitor<OpenIdConnectOptions>>()
                        ?.Get(OpenIdConnectDefaults.AuthenticationScheme);
                    OpenIdConnectConfiguration? configuration = oidcOptions?.ConfigurationManager is null
                        ? null
                        : await oidcOptions.ConfigurationManager.GetConfigurationAsync(context.HttpContext.RequestAborted);

                    // Keycloak (dev) has no persistent volume - a restart wipes its
                    // signing keys and sessions without advancing the saved id_token's
                    // own exp claim (doc/CHRONICLE.md). Always confirmed against
                    // Keycloak here rather than trusting the exp claim alone, on every
                    // authenticated request - not just at sign-out - since a fresh
                    // id_token is needed elsewhere too (upcoming Seller Portal profile
                    // work). Entra External ID's production URLs are assumed stable, so
                    // this is skipped there.
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

        if (!isBuildTimeOpenApiGeneration)
        {
            // Used only by AddCookie's OnValidatePrincipal above, to call the identity
            // provider's token endpoint directly for a refresh-token grant.
            builder.Services.AddHttpClient();

            authenticationBuilder.AddOpenIdConnect(options =>
            {
                // Set by AppHost.cs (ADR 0002/ADR 0010/ADR 0020): "Keycloak" locally,
                // "EntraExternalId" once deployed. Falls through to Keycloak (not a
                // throw) when absent, since the test host doesn't set it and Keycloak
                // was always the default.
                string? identityProvider = builder.Configuration["Identity:Provider"];

                switch (identityProvider)
                {
                    case KnownNames.IdentityProviderEntraExternalId:
                        string entraTenantSubdomain = builder.Configuration["Identity:Entra:TenantSubdomain"]
                            ?? throw new InvalidOperationException("The 'Identity:Entra:TenantSubdomain' configuration value is missing.");
                        options.Authority = $"https://{entraTenantSubdomain}.ciamlogin.com/";
                        options.ClientId = builder.Configuration["Identity:Entra:ClientId"]
                            ?? throw new InvalidOperationException("The 'Identity:Entra:ClientId' configuration value is missing.");
                        options.ClientSecret = builder.Configuration["Identity:Entra:ClientSecret"];

                        // Entra's App Roles surface as a "roles" claim (a JSON array) -
                        // MapJsonKey adds one Role claim per element, same pattern as
                        // Keycloak's "role" claim below, so the "SellerOnly" policy's
                        // RequireRole("Seller") works unchanged regardless of provider.
                        options.ClaimActions.MapJsonKey(ClaimTypes.Role, "roles");
                        break;

                    default: // KnownNames.IdentityProviderKeycloak, or absent (test host)
                        // Resolved lazily: the Keycloak endpoint, injected via Aspire
                        // service discovery. Configuration key is "...https:0", not
                        // "http" - Aspire upgrades Keycloak's http endpoint to https for
                        // local dev certs.
                        string keycloakBaseUrl = builder.Configuration["services:keycloak:https:0"]
                            ?? throw new InvalidOperationException("The 'services:keycloak:https:0' configuration value is missing.");
                        options.Authority = $"{keycloakBaseUrl}/realms/{KnownNames.KeycloakRealmEShop}";
                        options.ClientId = KnownNames.SellerPortalOidcClientId;
                        options.ClientSecret = builder.Configuration["Keycloak:ClientSecret"];

                        // Surfaces the "Seller" realm role as a standard ClaimTypes.Role
                        // claim (a JSON array of roles) so the "SellerOnly" policy's
                        // RequireRole works.
                        options.ClaimActions.MapJsonKey(ClaimTypes.Role, "role");
                        break;
                }

                options.ResponseType = OpenIdConnectResponseType.Code;
                options.CallbackPath = "/bff/signin-oidc";
                options.SignedOutCallbackPath = "/bff/signout-callback-oidc";
                options.SaveTokens = true;
                options.RequireHttpsMetadata = builder.Environment.IsProduction();

                options.Events.OnTicketReceived = context =>
                {
                    if (context.Properties is { } properties)
                    {
                        OidcTokenPruning.StripUnusedTokens(properties);
                    }

                    return Task.CompletedTask;
                };
            });
        }

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("SellerOnly", policy => policy.RequireRole("Seller"));

        // Skipped in "Testing" (no real IConnectionMultiplexer to bind the key ring's
        // XmlRepository to) and during build-time OpenAPI generation, matching the
        // resource-client guard in Program.cs. Deferred via AddOptions since a real
        // IConnectionMultiplexer doesn't exist yet at this point in the builder, same as
        // CookieAuthenticationOptions.SessionStore below.
        if (!builder.Environment.IsTestingEnvironment() && !isBuildTimeOpenApiGeneration)
        {
            builder.Services.AddOptions<KeyManagementOptions>()
                .Configure<IConnectionMultiplexer>((options, redis) =>
                    options.XmlRepository = new RedisXmlRepository(() => redis.GetDatabase(), "DataProtection-Keys"));
        }

        // SetApplicationName guards against key material ever being shared if another
        // app pointed at the same Redis/Azure Cache for Redis instance later. The
        // actual Redis-backed XmlRepository is set above, inside the "Testing" guard.
        builder.Services.AddDataProtection()
            .SetApplicationName("EShop.SellerPortal");

        // HybridCache itself is registered in Program.cs, as general infrastructure.
        // See HybridCacheTicketStore for why the ticket store is built on it.
        builder.Services.AddSingleton<ITicketStore, HybridCacheTicketStore>();
        builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<ITicketStore>((options, store) => options.SessionStore = store);
    }
}
