// <copyright file="KeycloakDiscoveryTests.cs" company="Henrik Jensen">
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

using System.Text.Json;
using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;

namespace Hj.EShop.AppHost.E2ETests;

// Starts the real AppHost and drives headless Chromium against it, same shape as
// SellerPortalLoginTests - see that file's own remarks. Proves PLAN-2.md §5.3/§6.2 item
// 4 directly: Keycloak's own discovery document (not just a successful login, which
// only proves the OIDC handler accepted whatever issuer it got) names the public
// reverse-proxy identity host as both its "issuer" and its endpoint hosts.
public sealed class KeycloakDiscoveryTests
{
    [Fact]
    public async Task DiscoveryDocumentUsesTheReverseProxyIdentityHostAsIssuerAndEndpointHost()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using E2ETestSession session = await E2ETestHarness.StartAsync(
            [KnownNames.ResourceKeycloak, KnownNames.ResourceDevReverseProxy], cancellationToken);

        Uri identityBaseAddress = session.GetBaseAddress(KnownNames.ResourceKeycloak);
        IPage page = await session.NewPageAsync();

        IAPIResponse response = await page.APIRequest.GetAsync(
            new Uri(identityBaseAddress, $"realms/{KnownNames.KeycloakRealmEShop}/.well-known/openid-configuration").ToString(),
            new APIRequestContextOptions { Timeout = (float)E2ETestHarness.DefaultTimeout.TotalMilliseconds });
        Assert.True(response.Ok, $"Expected a successful discovery response, got {response.Status}.");

        JsonElement? document = await response.JsonAsync();
        Assert.NotNull(document);

        string expectedOrigin = identityBaseAddress.GetLeftPart(UriPartial.Authority);
        Assert.Equal(KnownValues.KeycloakAuthority, document.Value.GetProperty("issuer").GetString());
        Assert.StartsWith(expectedOrigin, document.Value.GetProperty("authorization_endpoint").GetString(), StringComparison.Ordinal);
        Assert.StartsWith(expectedOrigin, document.Value.GetProperty("token_endpoint").GetString(), StringComparison.Ordinal);
        Assert.StartsWith(expectedOrigin, document.Value.GetProperty("end_session_endpoint").GetString(), StringComparison.Ordinal);
    }
}
