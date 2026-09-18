// <copyright file="ForwardedOriginTrustTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;

namespace Hj.EShop.AppHost.E2ETests;

// Starts the real AppHost and drives headless Chromium against it, same shape as
// SellerPortalLoginTests - see that file's own remarks. Proves PLAN-2.md §5/§6.2 item 6:
// a request that reaches the real reverse-proxy public host with a spoofed
// X-Forwarded-Host/-Proto still produces a valid OIDC challenge built from the true
// public origin, because the reverse proxy (forwardPublicOrigin: true, AppHost.cs)
// overwrites those headers before Seller Portal Web/Storefront - or, for Seller Portal,
// the Next.js route handler in front of the Bff - ever see the request.
//
// The .NET OIDC handler defaults to Pushed Authorization Requests against Keycloak 26
// (confirmed live: the visible redirect is only "...auth?client_id=...&request_uri=urn:
// ietf:params:oauth:request_uri:...", never a literal redirect_uri query value), so the
// redirect_uri itself is never visible to this browser-facing assertion - it went
// server-to-server in the PAR push. Proving it anyway relies on Keycloak's own
// server-side validation: the static realm client (eshop-realm.json) registers only the
// one real redirect URI, so Keycloak's PAR endpoint would have rejected an
// "https://evil.example.com/..." redirect_uri outright instead of minting a
// request_uri - a successful, error-free redirect is itself proof the app server built
// the PAR push from the true origin, not the spoofed header.
public sealed class ForwardedOriginTrustTests
{
    private const string MaliciousHost = "evil.example.com";

    [Fact]
    public async Task MaliciousForwardedHostCannotHijackSellerPortalOidcCallbackOrigin()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using E2ETestSession session = await E2ETestHarness.StartAsync(
            [KnownNames.ResourceSellerPortalBff, KnownNames.ResourceSellerPortalWeb, KnownNames.ResourceDevReverseProxy], cancellationToken);

        Uri webBaseAddress = session.GetBaseAddress(KnownNames.ResourceSellerPortalWeb);
        IPage page = await session.NewPageAsync();

        await AssertChallengeIsNotHijackedAsync(page, new Uri(webBaseAddress, "bff/login"), KnownNames.SellerPortalOidcClientId);
    }

    [Fact]
    public async Task MaliciousForwardedHostCannotHijackStorefrontOidcCallbackOrigin()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using E2ETestSession session = await E2ETestHarness.StartAsync(
            [KnownNames.ResourceStorefrontWeb, KnownNames.ResourceDevReverseProxy], cancellationToken);

        Uri webBaseAddress = session.GetBaseAddress(KnownNames.ResourceStorefrontWeb);
        IPage page = await session.NewPageAsync();

        await AssertChallengeIsNotHijackedAsync(page, new Uri(webBaseAddress, "ui/cms"), KnownNames.StorefrontOidcClientId);
    }

    private static async Task AssertChallengeIsNotHijackedAsync(IPage page, Uri challengeUri, string expectedClientId)
    {
        IAPIResponse response = await page.APIRequest.GetAsync(
            challengeUri.ToString(),
            new APIRequestContextOptions
            {
                Headers = new Dictionary<string, string>
                {
                    ["X-Forwarded-Host"] = MaliciousHost,
                    ["X-Forwarded-Proto"] = "http",
                },
                MaxRedirects = 0,
                Timeout = (float)E2ETestHarness.DefaultTimeout.TotalMilliseconds,
            });

        Assert.True(response.Status is >= 300 and < 400, $"Expected a redirect to Keycloak, got {response.Status}.");
        Assert.True(response.Headers.TryGetValue("location", out string? location), "Expected a Location header.");
        Assert.DoesNotContain(MaliciousHost, location, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            $"https://{KnownNames.ReverseProxyIdentityHostName}:{KnownNames.ReverseProxyHttpsPort}/realms/{KnownNames.KeycloakRealmEShop}/protocol/openid-connect/auth",
            location,
            StringComparison.Ordinal);
        Assert.Contains($"client_id={expectedClientId}", location, StringComparison.Ordinal);

        // No "error=" - Keycloak's PAR endpoint accepted the pushed redirect_uri, which
        // it would only do for the one real, statically registered URI.
        Assert.Contains("request_uri=", location, StringComparison.Ordinal);
        Assert.DoesNotContain("error=", location, StringComparison.Ordinal);
    }
}
