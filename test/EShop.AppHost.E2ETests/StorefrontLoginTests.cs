using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;

namespace Hj.EShop.AppHost.E2ETests;

// Starts the real AppHost and drives headless Chromium against it, same shape as
// SellerPortalLoginTests - see that file's own remarks. Exercises the Storefront's
// OIDC wiring and user synchronization by loading the authenticated CMS Shell.
public sealed class StorefrontLoginTests
{
    [Fact]
    public async Task LoggingInAsAStorefrontStaffActorCanLoadCms()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using E2ETestSession session = await E2ETestHarness.StartAsync(
            [KnownNames.ResourceStorefrontWeb, KnownNames.ResourceDevReverseProxy], cancellationToken);

        Uri webBaseAddress = session.GetBaseAddress(KnownNames.ResourceStorefrontWeb);
        IPage page = await session.NewPageAsync();

        await E2ETestHarness.LogInAsync(
            page,
            webBaseAddress,
            TestCredentials.StorefrontEditor.LoginPath,
            TestCredentials.StorefrontEditor.Username,
            TestCredentials.StorefrontEditor.Password);

        IResponse? cmsResponse = await page.GotoAsync(new Uri(webBaseAddress, "ui/cms").ToString());
        Assert.NotNull(cmsResponse);
        Assert.True(cmsResponse.Ok, $"Expected a successful CMS response, got {cmsResponse.Status}.");
    }
}
