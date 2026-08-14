// <copyright file="StorefrontAuthConfiguration.cs" company="Henrik Jensen">
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
using EPiServer.DependencyInjection;
using EPiServer.Security;
using Hj.EShop.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Hj.EShop.StoreFront.Web.Authentication;

// Storefront's own dual-provider OIDC wiring (Keycloak locally / Microsoft Entra
// External ID once deployed - ADR 0002/0010/0020), mirroring
// EShop.SellerPortal.Bff/Authentication/AuthConfiguration.cs's established pattern.
// Two differences from that pattern, both because Storefront is itself a server-rendered
// site (not an API behind a separate frontend, unlike the Bff): the cookie's default
// challenge/redirect behavior is left as-is rather than forced to a bare 401/403, and
// the OIDC callback paths are ASP.NET Core's own defaults, not "/bff/..."-prefixed.
//
// STOREFRONT-PLAN.md's Phase 1 gap this file closes: this project called neither
// AddCmsAspNetIdentity() nor EPiServer.OptimizelyIdentity's AddOptimizelyIdentity(), so
// no CMS user/role sync ever ran - EPiServer.Security.ISynchronizingUserService's
// SynchronizeAsync (public, not `.Internal` - the same public entry point
// AddOptimizelyIdentity() itself calls) is invoked here instead, on our own sign-in.
internal static class StorefrontAuthConfiguration
{
    public static void AddAuthConfiguration(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "storefront";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                // Populates Optimizely's own CMS user/role sync table on sign-in - see
                // the class comment above. Without this, role-based lookups elsewhere in
                // Optimizely (e.g. a future approval reviewer match) silently match
                // nothing, even though authorization policies here (RequireRole) work
                // from the claim alone.
                options.Events.OnSignedIn = async context =>
                {
                    if (context.Principal?.Identity is ClaimsIdentity identity)
                    {
                        await context.HttpContext.RequestServices
                            .GetRequiredService<ISynchronizingUserService>()
                            .SynchronizeAsync(identity);
                    }
                };
            })
            .AddOpenIdConnect(options =>
            {
                // Set by AppHost.cs (ADR 0002/ADR 0010/ADR 0020): "Keycloak" locally,
                // "EntraExternalId" once deployed. Falls through to Keycloak (not a
                // throw) when absent, same reasoning as the Bff's own switch.
                string? identityProvider = configuration["Identity:Provider"];

                switch (identityProvider)
                {
                    case KnownNames.IdentityProviderEntraExternalId:
                        string entraTenantSubdomain = configuration["Identity:Entra:TenantSubdomain"]
                            ?? throw new InvalidOperationException("The 'Identity:Entra:TenantSubdomain' configuration value is missing.");
                        options.Authority = $"https://{entraTenantSubdomain}.ciamlogin.com/";
                        options.ClientId = configuration["Identity:Entra:ClientId"]
                            ?? throw new InvalidOperationException("The 'Identity:Entra:ClientId' configuration value is missing.");
                        options.ClientSecret = configuration["Identity:Entra:ClientSecret"];

                        // Entra's App Roles surface as a "roles" claim (a JSON array) -
                        // MapJsonKey adds one Role claim per element, same pattern as
                        // Keycloak's "role" claim below, so RequireRole(...) policies
                        // work unchanged regardless of provider.
                        options.ClaimActions.MapJsonKey(ClaimTypes.Role, "roles");
                        break;

                    default: // KnownNames.IdentityProviderKeycloak, or absent (test host)
                        // Resolved lazily: the Keycloak endpoint, injected via Aspire
                        // service discovery. Configuration key is "...https:0", not
                        // "http" - Aspire upgrades Keycloak's http endpoint to https for
                        // local dev certs.
                        string keycloakBaseUrl = configuration["services:keycloak:https:0"]
                            ?? throw new InvalidOperationException("The 'services:keycloak:https:0' configuration value is missing.");
                        options.Authority = $"{keycloakBaseUrl}/realms/{KnownNames.KeycloakRealmEShop}";
                        options.ClientId = KnownNames.StorefrontOidcClientId;
                        options.ClientSecret = configuration["Keycloak:ClientSecret"];

                        // Surfaces every realm role as a standard ClaimTypes.Role claim
                        // (a JSON array), via the "storefront-role" client scope
                        // KeycloakStorefrontClientProvisioner attaches.
                        options.ClaimActions.MapJsonKey(ClaimTypes.Role, "role");
                        break;
                }

                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SaveTokens = true;
                options.RequireHttpsMetadata = webHostEnvironment.IsProduction();

                // Keycloak issues "preferred_username" by default; Entra External ID's
                // CIAM user flows do too - one stable claim, present under both
                // providers, chosen because Optimizely's IPrincipalAccessor.Name (via
                // Identity.Name) is the ONLY identity carried through every "who did
                // this" field (SavedBy/StartedBy/CompletedBy/ApprovalStepDecision
                // .Username) - this choice is effectively permanent once content exists.
                // Same claim EPiServer.OptimizelyIdentity's own AddOptimizelyIdentity()
                // uses, for the same reason.
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                };
            });

        // Maps external roles onto Optimizely's own role vocabulary via the public
        // AddMappedRole extension - the same supported technique Optimizely's own
        // EPiServer.Commerce.Authorization.MapVirtualRoles uses. "Merchandisers" is not
        // an Optimizely built-in role; nothing in this phase consumes it yet, but it's
        // reserved now so the future Merchandiser review tool (STOREFRONT-PLAN.md) has
        // a role to gate its own authorization policy on without redoing this mapping.
        // Marketer/CustomerServiceAgent have realm roles (KeycloakStorefrontClientProvisioner)
        // but no Optimizely-side mapping yet - out of this phase's explicit scope.
        services.AddMappedRole("CmsEditors", ["ContentEditor"]);
        services.AddMappedRole("CmsAdmins", ["SiteAdministrator"]);
        services.AddMappedRole("CatalogManagers", ["Merchandiser"]);
        services.AddMappedRole("Merchandisers", ["Merchandiser"]);
    }
}
