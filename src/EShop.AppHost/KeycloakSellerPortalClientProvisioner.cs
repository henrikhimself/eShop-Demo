// <copyright file="KeycloakSellerPortalClientProvisioner.cs" company="Henrik Jensen">
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

// See doc/CHRONICLE.md — System invariants (redirect_uri matched by exact string) and
// Aspire hosting integration quirks (Keycloak has no persistent volume). The generic
// token/role/scope/client mechanics live in KeycloakAdminApiClient, shared with
// KeycloakStorefrontClientProvisioner - only the Seller Portal-specific shape (role
// name, callback paths) stays here.
internal static class KeycloakSellerPortalClientProvisioner
{
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
            ?? throw new InvalidOperationException("The Seller Portal OIDC client secret parameter has no value.");

        // See doc/CHRONICLE.md — Aspire hosting integration quirks (GetEndpoint("http")
        // stays named "http" after an https upgrade).
        using HttpClient keycloakHttpClient = await KeycloakAdminApiClient.CreateAuthorizedHttpClientAsync(
            new Uri(keycloak.GetEndpoint("http").Url), username, password, cancellationToken);

        // The "Seller" realm role a Site Administrator assigns in Keycloak to approve a
        // Seller (see doc/SPEC.md).
        string sellerRoleId = await KeycloakAdminApiClient.CreateRealmRoleAsync(
            keycloakHttpClient,
            realm,
            "Seller",
            "Granted by a Site Administrator in Keycloak to approve a user as a Seller Portal Seller.",
            cancellationToken);
        string sellerRoleScopeId = await KeycloakAdminApiClient.CreateRoleClaimClientScopeAsync(
            keycloakHttpClient, realm, "seller-role", "role", cancellationToken);

        // "test-seller" is the only user Realms/eshop-realm.json seeds with this role in
        // mind - assigned here, not via that static import's "realmRoles" field, since
        // the role does not exist yet at import time (it is created above, after
        // import, via the admin API).
        await KeycloakAdminApiClient.AssignRealmRoleToUserAsync(
            keycloakHttpClient, realm, "test-seller", sellerRoleId, "Seller", cancellationToken);

        string origin = webBaseAddress.ToString().TrimEnd('/');
        var clientRepresentation = new
        {
            clientId,
            name = "Seller Portal BFF",
            enabled = true,
            protocol = "openid-connect",
            publicClient = false,
            secret,
            standardFlowEnabled = true,
            directAccessGrantsEnabled = false,
            implicitFlowEnabled = false,
            serviceAccountsEnabled = false,
            redirectUris = new[] { new Uri(webBaseAddress, "bff/signin-oidc").ToString() },
            webOrigins = new[] { origin },
            attributes = new Dictionary<string, string>
            {
                // See doc/CHRONICLE.md — Keycloak matches redirectUris/
                // post.logout.redirect.uris by exact string, including path.
                ["post.logout.redirect.uris"] = new Uri(webBaseAddress, "bff/signout-callback-oidc").ToString(),
            },
        };

        string clientUuid = await KeycloakAdminApiClient.CreateClientAsync(keycloakHttpClient, realm, clientRepresentation, cancellationToken);
        await KeycloakAdminApiClient.AttachDefaultClientScopeAsync(keycloakHttpClient, realm, clientUuid, sellerRoleScopeId, cancellationToken);
    }
}
