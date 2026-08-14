// <copyright file="KeycloakStorefrontClientProvisioner.cs" company="Henrik Jensen">
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

namespace Hj.EShop.AppHost;

// Mirrors KeycloakSellerPortalClientProvisioner's shape (both share the generic
// mechanics in KeycloakAdminApiClient), but provisions Storefront's non-Shopper staff
// roles instead of the single "Seller" role: one realm role per SPEC.md actor, each
// assigned to its already-seeded Realms/eshop-realm.json test user. A dedicated
// "storefront-role" client scope, not the Seller Portal's "seller-role" one, so this
// provisioner has no ordering dependency on the Seller Portal's own - both scopes'
// mapper configuration is otherwise identical (every realm role -> a "role" claim).
internal static class KeycloakStorefrontClientProvisioner
{
    // (Keycloak username, realm role name) - the realm role name matches the
    // AddMappedRole source role name EShop.StoreFront.Web's own OIDC configuration
    // expects (see doc/CHRONICLE.md/STOREFRONT-PLAN.md).
    private static readonly IReadOnlyList<(string Username, string RoleName)> _staffRoleAssignments =
    [
        ("test-content-editor", "ContentEditor"),
        ("test-marketer", "Marketer"),
        ("test-merchandiser", "Merchandiser"),
        ("test-site-administrator", "SiteAdministrator"),
        ("test-customer-service-agent", "CustomerServiceAgent"),
    ];

    public static async Task ProvisionAsync(
        IResourceBuilder<KeycloakResource> keycloak,
        IResourceBuilder<ParameterResource> adminUsername,
        IResourceBuilder<ParameterResource> adminPassword,
        IResourceBuilder<ParameterResource> clientSecret,
        Uri webBaseAddress,
        string clientId,
        string realm,
        CancellationToken cancellationToken)
    {
        string username = await adminUsername.Resource.GetValueAsync(cancellationToken)
            ?? throw new InvalidOperationException("The Keycloak admin username parameter has no value.");
        string password = await adminPassword.Resource.GetValueAsync(cancellationToken)
            ?? throw new InvalidOperationException("The Keycloak admin password parameter has no value.");
        string secret = await clientSecret.Resource.GetValueAsync(cancellationToken)
            ?? throw new InvalidOperationException("The Storefront OIDC client secret parameter has no value.");

        using HttpClient keycloakHttpClient = await KeycloakAdminApiClient.CreateAuthorizedHttpClientAsync(
            new Uri(keycloak.GetEndpoint("http").Url), username, password, cancellationToken);

        string storefrontRoleScopeId = await KeycloakAdminApiClient.CreateRoleClaimClientScopeAsync(
            keycloakHttpClient, realm, "storefront-role", "role", cancellationToken);

        foreach ((string assignedUsername, string roleName) in _staffRoleAssignments)
        {
            string roleId = await KeycloakAdminApiClient.CreateRealmRoleAsync(
                keycloakHttpClient, realm, roleName, $"Granted to a Storefront '{roleName}' actor, per doc/SPEC.md.", cancellationToken);
            await KeycloakAdminApiClient.AssignRealmRoleToUserAsync(
                keycloakHttpClient, realm, assignedUsername, roleId, roleName, cancellationToken);
        }

        string origin = webBaseAddress.ToString().TrimEnd('/');
        var clientRepresentation = new
        {
            clientId,
            name = "Storefront",
            enabled = true,
            protocol = "openid-connect",
            publicClient = false,
            secret,
            standardFlowEnabled = true,
            directAccessGrantsEnabled = false,
            implicitFlowEnabled = false,
            serviceAccountsEnabled = false,

            // ASP.NET Core's OpenIdConnectHandler default callback paths - not
            // overridden the way the Bff overrides them to a "/bff/..." prefix, since
            // Storefront is not proxied behind a separate frontend the way the Bff is.
            redirectUris = new[] { new Uri(webBaseAddress, "signin-oidc").ToString() },
            webOrigins = new[] { origin },
            attributes = new Dictionary<string, string>
            {
                // See doc/CHRONICLE.md — Keycloak matches redirectUris/
                // post.logout.redirect.uris by exact string, including path.
                ["post.logout.redirect.uris"] = new Uri(webBaseAddress, "signout-callback-oidc").ToString(),
            },
        };

        string clientUuid = await KeycloakAdminApiClient.CreateClientAsync(keycloakHttpClient, realm, clientRepresentation, cancellationToken);
        await KeycloakAdminApiClient.AttachDefaultClientScopeAsync(
            keycloakHttpClient, realm, clientUuid, storefrontRoleScopeId, cancellationToken);
    }
}
