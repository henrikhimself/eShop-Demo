// <copyright file="KeycloakAdminApiClient.cs" company="Henrik Jensen">
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

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hj.EShop.AppHost;

// Generic Keycloak admin-API operations shared by every per-app client provisioner
// (KeycloakSellerPortalClientProvisioner, KeycloakStorefrontClientProvisioner) -
// extracted once a second provisioner needed the exact same token/role/scope/client
// dance. See doc/CHRONICLE.md — System invariants (redirect_uri matched by exact
// string) and Aspire hosting integration quirks (Keycloak has no persistent volume).
internal static class KeycloakAdminApiClient
{
    public static async Task<HttpClient> CreateAuthorizedHttpClientAsync(
        Uri keycloakBaseAddress, string adminUsername, string adminPassword, CancellationToken cancellationToken)
    {
        HttpClient client = new() { BaseAddress = keycloakBaseAddress };
        try
        {
            using FormUrlEncodedContent tokenRequestContent = new(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = adminUsername,
                ["password"] = adminPassword,
            });
            using HttpResponseMessage tokenResponse = await client.PostAsync(
                "realms/master/protocol/openid-connect/token", tokenRequestContent, cancellationToken);
            tokenResponse.EnsureSuccessStatusCode();
            Dictionary<string, object>? tokenJson =
                await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken);
            string accessToken = GetRequiredField(tokenJson, "access_token", "the Keycloak admin token response");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    // See doc/CHRONICLE.md — a Keycloak realm import replaces, not merges,
    // roles/clientScopes; these are created via the admin API instead. Returns the
    // created role's id (its representation has no separate "self" Location header the
    // way client/client-scope creation does - role representations are looked up by
    // name instead).
    public static async Task<string> CreateRealmRoleAsync(
        HttpClient keycloakHttpClient, string realm, string roleName, string description, CancellationToken cancellationToken)
    {
        var roleRepresentation = new { name = roleName, description };
        using HttpResponseMessage createResponse = await keycloakHttpClient.PostAsJsonAsync(
            $"admin/realms/{realm}/roles", roleRepresentation, cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        using HttpResponseMessage getResponse = await keycloakHttpClient.GetAsync(
            $"admin/realms/{realm}/roles/{roleName}", cancellationToken);
        getResponse.EnsureSuccessStatusCode();
        Dictionary<string, object>? role =
            await getResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken);
        return GetRequiredField(role, "id", $"the created '{roleName}' role response");
    }

    // Surfaces every realm role a user holds as a flat claim (matches each app's own
    // MapJsonKey(ClaimTypes.Role, claimName)) instead of the default nested
    // realm_access.roles. Returns the scope's id.
    public static async Task<string> CreateRoleClaimClientScopeAsync(
        HttpClient keycloakHttpClient, string realm, string scopeName, string claimName, CancellationToken cancellationToken)
    {
        var scopeRepresentation = new
        {
            name = scopeName,
            protocol = "openid-connect",
            protocolMappers = new[]
            {
                new
                {
                    name = $"{scopeName}-mapper",
                    protocol = "openid-connect",
                    protocolMapper = "oidc-usermodel-realm-role-mapper",
                    config = new Dictionary<string, string>
                    {
                        ["user.attribute"] = "foo",
                        ["claim.name"] = claimName,
                        ["jsonType.label"] = "String",
                        ["multivalued"] = "true",
                        ["id.token.claim"] = "true",
                        ["access.token.claim"] = "true",
                        ["userinfo.token.claim"] = "true",
                    },
                },
            },
        };
        using HttpResponseMessage createResponse = await keycloakHttpClient.PostAsJsonAsync(
            $"admin/realms/{realm}/client-scopes", scopeRepresentation, cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        return createResponse.Headers.Location?.Segments[^1]
            ?? throw new InvalidOperationException("The Keycloak client scope creation response has no Location header.");
    }

    // Returns the created client's uuid.
    public static async Task<string> CreateClientAsync(
        HttpClient keycloakHttpClient, string realm, object clientRepresentation, CancellationToken cancellationToken)
    {
        using HttpResponseMessage createResponse = await keycloakHttpClient.PostAsJsonAsync(
            $"admin/realms/{realm}/clients", clientRepresentation, cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        return createResponse.Headers.Location?.Segments[^1]
            ?? throw new InvalidOperationException("The Keycloak client creation response has no Location header.");
    }

    // Attached directly to the given client, not via the realm's global
    // defaultDefaultClientScopes, to avoid risking built-in scopes for other future
    // clients.
    public static async Task AttachDefaultClientScopeAsync(
        HttpClient keycloakHttpClient, string realm, string clientUuid, string scopeId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage attachResponse = await keycloakHttpClient.PutAsync(
            $"admin/realms/{realm}/clients/{clientUuid}/default-client-scopes/{scopeId}", null, cancellationToken);
        attachResponse.EnsureSuccessStatusCode();
    }

    public static async Task AssignRealmRoleToUserAsync(
        HttpClient keycloakHttpClient, string realm, string username, string roleId, string roleName, CancellationToken cancellationToken)
    {
        using HttpResponseMessage usersResponse = await keycloakHttpClient.GetAsync(
            $"admin/realms/{realm}/users?username={username}&exact=true", cancellationToken);
        usersResponse.EnsureSuccessStatusCode();
        List<Dictionary<string, object>>? users =
            await usersResponse.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>(cancellationToken);
        Dictionary<string, object> user = users?.Find(
            u => u.TryGetValue("username", out object? foundUsername) && foundUsername?.ToString() == username)
            ?? throw new InvalidOperationException($"The realm has no '{username}' user to assign the '{roleName}' role to.");

        string userId = GetRequiredField(user, "id", $"the found '{username}' user");

        var roleMapping = new[] { new { id = roleId, name = roleName } };
        using HttpResponseMessage assignResponse = await keycloakHttpClient.PostAsJsonAsync(
            $"admin/realms/{realm}/users/{userId}/role-mappings/realm", roleMapping, cancellationToken);
        assignResponse.EnsureSuccessStatusCode();
    }

    // Keycloak's admin API returns a Dictionary<string, object>; this surfaces a
    // diagnostic naming the missing field and the raw response instead of a bare
    // KeyNotFoundException.
    public static string GetRequiredField(Dictionary<string, object>? source, string fieldName, string context)
    {
        if (source is not null && source.TryGetValue(fieldName, out object? value) && value?.ToString() is { } text)
        {
            return text;
        }

        string raw = source is null ? "(no response body)" : JsonSerializer.Serialize(source);
        throw new InvalidOperationException($"Keycloak response missing expected field '{fieldName}' in {context}: {raw}");
    }
}
