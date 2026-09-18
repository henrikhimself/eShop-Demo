using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;

namespace Hj.EShop.AppHost.E2ETests;

// Starts the real AppHost - BFF, Keycloak, SQL Server, the Service Bus and Storage
// emulators - and drives headless Chromium against it. Much slower than the rest of the
// test suite and needs Docker plus Playwright's browser binaries, so this project is
// deliberately not part of EShop.slnx: run it via `eshop test e2e`, not `eshop test`.
public sealed class SellerPortalLoginTests
{
    [Fact]
    public async Task LoggingInAsASellerReachesAnAuthenticatedDraftsEndpoint()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using E2ETestSession session = await E2ETestHarness.StartAsync(
            [KnownNames.ResourceSellerPortalBff, KnownNames.ResourceSellerPortalWeb, KnownNames.ResourceDevReverseProxy], cancellationToken);

        Uri webBaseAddress = session.GetBaseAddress(KnownNames.ResourceSellerPortalWeb);
        IPage page = await session.NewPageAsync();

        await E2ETestHarness.LogInAsync(
            page,
            webBaseAddress,
            TestCredentials.SellerPortalSeller.LoginPath,
            TestCredentials.SellerPortalSeller.Username,
            TestCredentials.SellerPortalSeller.Password);

        // The auth ticket lives in HybridCache/Valkey, so the cookie should be short
        // and opaque, and the old seller-portalC1/C2 chunk cookies must be absent.
        IReadOnlyList<BrowserContextCookiesResult> cookies = await page.Context.CookiesAsync();
        BrowserContextCookiesResult sellerPortalCookie = Assert.Single(cookies, c => c.Name == "seller-portal");
        Assert.True(
            sellerPortalCookie.Value.Length < 1000,
            $"Expected a short opaque ticket-store key, got {sellerPortalCookie.Value.Length} chars.");
        Assert.DoesNotContain(cookies, c => c.Name is "seller-portalC1" or "seller-portalC2");

        // Exercises the full public path: Next.js's /bff/* proxy forwards to the BFF's
        // /bff/api/drafts, gated by the "SellerOnly" policy. Uses the page's own request
        // context so the session cookie from the login above is attached.
        IAPIResponse draftsResponse = await page.APIRequest.GetAsync(
            new Uri(webBaseAddress, "bff/api/drafts").ToString(),
            new APIRequestContextOptions { Timeout = (float)E2ETestHarness.DefaultTimeout.TotalMilliseconds });
        Assert.True(draftsResponse.Ok, $"Expected a successful response, got {draftsResponse.Status}.");

        // Logout should succeed through the configured post-logout redirect URI.
        // Navigates directly rather than clicking a "Log out" link - /bff/logout is a
        // Route Handler that redirects, same reasoning as the login link above, and
        // this way the assertion doesn't depend on which page happens to render the nav.
        await E2ETestHarness.LogOutAsync(page, webBaseAddress);

        IAPIResponse draftsAfterLogoutResponse = await page.APIRequest.GetAsync(
            new Uri(webBaseAddress, "bff/api/drafts").ToString(),
            new APIRequestContextOptions { Timeout = (float)E2ETestHarness.DefaultTimeout.TotalMilliseconds });
        Assert.Equal(401, draftsAfterLogoutResponse.Status);
    }
}
