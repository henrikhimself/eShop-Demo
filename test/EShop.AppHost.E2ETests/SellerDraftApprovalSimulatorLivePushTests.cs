using System.Text.RegularExpressions;
using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Hj.EShop.AppHost.E2ETests;

// Exercises the DevTools Seller Draft Approval Simulator's SignalR-driven live update
// (ADR 0015): an already-open, idle browser tab reflects a new submission, and later
// its approval, with no reload or navigation of its own. This proves the
// "submissionsChanged" broadcast reaches a connected client, not just that a later
// REST re-fetch would eventually show the same data, matching the dedicated
// toast-visibility assertion in SellerPortalMovieDraftSubmissionTests. Also exercises
// the pending row's image thumbnail and its click-to-open-full-image link. Not part of
// EShop.slnx - run via `eshop test e2e`, not `eshop test`.
public sealed class SellerDraftApprovalSimulatorLivePushTests
{
    [Fact]
    public async Task SubmittingAndApprovingADraft_UpdatesAnAlreadyOpenObserverPageWithNoReloadOfItsOwn()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string title = $"E2E Live Push Movie {Guid.NewGuid():N}";

        await using E2ETestSession session = await E2ETestHarness.StartAsync(
            [KnownNames.ResourceSellerPortalBff, KnownNames.ResourceSellerPortalWeb, KnownNames.ResourceDevTools, KnownNames.ResourceDevReverseProxy],
            cancellationToken);

        Uri webBaseAddress = session.GetBaseAddress(KnownNames.ResourceSellerPortalWeb);
        Uri devToolsBaseAddress = session.GetBaseAddress(KnownNames.ResourceDevTools);

        IPage observerPage = await session.NewPageAsync();

        // Opened once, before anything is submitted, and never navigated again - the
        // whole point of this test is that this page updates on its own.
        await observerPage.GotoAsync(devToolsBaseAddress.ToString());
        await observerPage.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Seller Draft Approval Simulator" })
            .First.ClickAsync();

        IPage sellerPage = await session.NewPageAsync();

        await E2ETestHarness.LogInAsync(
            sellerPage,
            webBaseAddress,
            TestCredentials.SellerPortalSeller.LoginPath,
            TestCredentials.SellerPortalSeller.Username,
            TestCredentials.SellerPortalSeller.Password);
        await E2ETestHarness.WaitForDraftsPageReadyAsync(sellerPage);

        IResponse createDraftResponse = await E2ETestHarness.ClickAndWaitForDraftCreationResponseAsync(
            sellerPage,
            "/bff/api/drafts/movies",
            async () => await sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New movie draft" }).ClickAsync());
        Assert.True(createDraftResponse.Ok, $"Expected movie draft creation to succeed, got {createDraftResponse.Status}.");
        await Expect(sellerPage).ToHaveURLAsync(new Regex(@"/drafts/movies/[0-9a-fA-F-]+$"));

        await sellerPage.Locator("#title").FillAsync(title);
        await sellerPage.Locator("#genre").FillAsync("Science Fiction");
        await sellerPage.Locator("#description").FillAsync("An E2E test fixture movie.");
        await sellerPage.Locator("#yearOfRelease").FillAsync("2020");

        await sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add format variant" }).ClickAsync();
        await sellerPage.Locator("input[aria-label='Price']").FillAsync("9.99");
        await sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save changes" }).ClickAsync();
        await Expect(sellerPage.Locator("h1")).ToHaveTextAsync(title);

        string fixtureImagePath = Path.Combine(AppContext.BaseDirectory, "assets", "cover-image.png");
        await sellerPage.Locator("input[type='file']").SetInputFilesAsync(fixtureImagePath);
        await Expect(sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Remove cover image" })).ToBeVisibleAsync();

        ILocator submitButton = sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Submit for review" });
        await Expect(submitButton).ToBeEnabledAsync();
        await submitButton.ClickAsync();

        // isDraftStatus flips to false once the draft leaves Draft status, disabling
        // every field - the observable signal that the submit call committed.
        await Expect(sellerPage.Locator("#title")).ToBeDisabledAsync();

        // No GotoAsync/ReloadAsync of observerPage from here on - only the hub push
        // (SellerDraftApprovalSimulatorConsumer's broadcast after store.Add, which the
        // page's own script turns into a location.reload()) can make this row appear.
        ILocator pendingRow = observerPage.Locator("tr", new PageLocatorOptions { HasText = title });
        await Expect(pendingRow).ToBeVisibleAsync();

        // The Images column's thumbnail for the cover image uploaded above - proves
        // the DevTools simulator's image-thumbnail feature, not just the pending row
        // itself.
        ILocator thumbnail = pendingRow.Locator("img.thumbnail");
        await Expect(thumbnail).ToBeVisibleAsync();

        // Clicking the thumbnail's link opens the full image in a new tab
        // (target="_blank") - proves IndexModel.OnGetImageAsync's authenticated blob
        // stream actually serves the image end to end, not just that the anchor/img
        // markup is present.
        IPage imagePopup = await observerPage.Context.RunAndWaitForPageAsync(async () =>
            await pendingRow.Locator("a").First.ClickAsync());
        await imagePopup.WaitForLoadStateAsync(LoadState.Load);
        Assert.Contains("handler=Image", imagePopup.Url, StringComparison.Ordinal);
        await Expect(imagePopup.Locator("img")).ToBeVisibleAsync();

        IPage actorPage = await session.NewPageAsync();
        await actorPage.GotoAsync(devToolsBaseAddress.ToString());
        await actorPage.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Seller Draft Approval Simulator" })
            .First.ClickAsync();
        await actorPage.Locator("tr", new PageLocatorOptions { HasText = title })
            .First.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Approve" }).ClickAsync();

        // Again, no action of any kind on observerPage - only the hub push
        // (IndexModel.OnPostApproveAsync's broadcast after store.Remove) can make this
        // row disappear.
        await Expect(pendingRow).Not.ToBeVisibleAsync();
    }
}
