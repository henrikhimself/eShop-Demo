using System.Text.Json;
using System.Text.RegularExpressions;
using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Hj.EShop.AppHost.E2ETests;

// Exercises the whole Movie draft loop for real: starts the AppHost (BFF, Web,
// DevTools, Keycloak, SQL Server, the Service Bus/Storage emulators), drives the
// Seller Portal UI as test-seller to create/fill/submit a draft, then drives the
// DevTools "Seller Draft Approval Simulator" (a second, independent browser page) to
// approve it, and confirms the Seller Portal side sees the Approved outcome with a
// SKU. Also keeps idle, never-reloaded observer tabs open on the Submissions page,
// the Inventory page, and this same draft's edit page throughout, proving each one's
// own SSE connection - not a REST re-fetch - is what makes it reflect the submit and
// the eventual approval outcome live. Not part of EShop.slnx - run via
// `eshop test e2e`, not `eshop test`.
public sealed class SellerPortalMovieDraftSubmissionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task SubmittingAMovieDraftAndApprovingItInDevToolsMarksItApprovedWithASku()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string title = $"E2E Movie {Guid.NewGuid():N}";

        // Waiting for the BFF also waits for SQL Server, Service Bus, Storage, and
        // Keycloak: the BFF's own /health check (its Aspire resource health check,
        // AppHost.cs) aggregates each dependency's Aspire-registered health check, so
        // it isn't Running/healthy until they all are. The Web and DevTools resources
        // have no such dependency wiring of their own, so they're awaited separately.
        await using E2ETestSession session = await E2ETestHarness.StartAsync(
            [KnownNames.ResourceSellerPortalBff, KnownNames.ResourceSellerPortalWeb, KnownNames.ResourceDevTools],
            cancellationToken);

        Uri webBaseAddress = session.GetBaseAddress(KnownNames.ResourceSellerPortalWeb);
        Uri devToolsBaseAddress = session.GetBaseAddress(KnownNames.ResourceDevTools);

        // An explicit context (rather than session.NewPageAsync's implicit
        // single-page context) is required so more tabs can be opened later sharing
        // sellerPage's cookies - IBrowserContext.NewPageAsync throws on a context
        // created via that implicit helper.
        IBrowserContext sellerContext = await session.Browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        IPage sellerPage = await sellerContext.NewPageAsync();
        E2ETestHarness.ApplyDefaultTimeouts(sellerPage);

        // Lands directly on /drafts - AuthEndpoints.cs's /bff/login challenge redirects
        // there on success, not to the landing page.
        await E2ETestHarness.LogInAsTestSellerAsync(sellerPage, webBaseAddress);

        // Captured once, right after login, so the observer tabs below can each get
        // their own IBrowserContext pre-authenticated with the same Seller session -
        // Chromium pools HTTP/1.1 connections per browser context, and this draft's
        // edit page, the Submissions page, and the Inventory page each hold a
        // long-lived SSE connection open for the rest of this test. Packing three or
        // more such connections into a single context (which is what sharing
        // sellerPage's own context would do) exhausts that pool's ~6-connections-per-origin
        // ceiling, indefinitely stalling any further request on that context -
        // including sellerPage's own draft-creation POST and later navigations.
        string sellerStorageState = await sellerContext.StorageStateAsync();

        async Task<IPage> OpenObserverPageAsync(string url)
        {
            IBrowserContext observerContext = await session.Browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                StorageState = sellerStorageState,
            });
            IPage observerPage = await observerContext.NewPageAsync();
            E2ETestHarness.ApplyDefaultTimeouts(observerPage);
            await observerPage.GotoAsync(url);
            return observerPage;
        }

        await sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New movie draft" }).ClickAsync();
        await Expect(sellerPage).ToHaveURLAsync(new Regex(@"/drafts/movies/[0-9a-fA-F-]+$"));

        // Each opened once up front - before this draft is submitted - and never
        // reloaded/navigated again from here on, proving the Submissions and
        // Inventory pages' own SSE connections, not a REST re-fetch, are what later
        // makes each show this draft's outcome live.
        IPage submissionsPage = await OpenObserverPageAsync(new Uri(webBaseAddress, "submissions").ToString());
        IPage inventoryPage = await OpenObserverPageAsync(new Uri(webBaseAddress, "inventory").ToString());

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

        // Proves SubmissionEndpoints.cs's submit route broadcasts to
        // SubmissionNotificationBroadcaster - submissionsPage was opened before this
        // draft existed and is never reloaded, so this row can only appear via its own
        // SSE connection.
        ILocator submissionsRow = submissionsPage.Locator("tr", new PageLocatorOptions { HasText = title });
        await Expect(submissionsRow).ToBeVisibleAsync();
        await Expect(submissionsRow).ToContainTextAsync("Pending");

        // A second, separately-authenticated observer on the same draft's edit page,
        // opened before approval and left untouched - proves the edit page's own SSE
        // connection (not just submissionsPage's) lives through PendingReview ->
        // PendingImageCleanup -> removed, ending in the "Approved" panel with no
        // reload of its own.
        IPage editObserverPage = await OpenObserverPageAsync(sellerPage.Url);

        IPage devToolsPage = await session.NewPageAsync();
        await devToolsPage.GotoAsync(devToolsBaseAddress.ToString());
        await devToolsPage.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Seller Draft Approval Simulator" })
            .First.ClickAsync();

        // The submission reaches the store asynchronously (via SellerDraftApprovalSimulatorConsumer
        // draining the queue) - poll by reloading rather than a one-shot check.
        ILocator pendingRow = devToolsPage.Locator("tr", new PageLocatorOptions { HasText = title });
        await E2ETestHarness.WaitUntilAsync(
            async () =>
            {
                if (await pendingRow.CountAsync() > 0)
                {
                    return true;
                }

                await devToolsPage.ReloadAsync();
                return false;
            },
            cancellationToken);

        await pendingRow.First.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Approve" }).ClickAsync();

        // Confirms the toast notification (SubmissionEvents.tsx, driven by the BFF's
        // SSE endpoint) actually reaches the Seller's page, not just the REST-visible
        // outcome below. Uses Expect's own auto-retrying poll, started immediately
        // after the click - well before the async Service Bus round trip publishes the
        // SSE event - rather than checking inside the REST-polling loop further down,
        // which risks the toast having already auto-dismissed (3s) by the time that
        // loop's 2s cadence happens to check.
        await Expect(sellerPage.GetByText(new Regex($"{Regex.Escape(title)}.*approved", RegexOptions.IgnoreCase)))
            .ToBeVisibleAsync();

        // The approval result also reaches the BFF asynchronously (via
        // SubmissionResultConsumer) - poll the seller's own submissions until it shows.
        string? assignedSku = null;
        await E2ETestHarness.WaitUntilAsync(
            async () =>
            {
                IAPIResponse response = await sellerPage.APIRequest.GetAsync(
                    new Uri(webBaseAddress, "bff/api/submissions").ToString(),
                    new APIRequestContextOptions { Timeout = (float)E2ETestHarness.DefaultTimeout.TotalMilliseconds });
                if (!response.Ok)
                {
                    return false;
                }

                List<SubmissionSummaryDto>? submissions = JsonSerializer.Deserialize<List<SubmissionSummaryDto>>(
                    await response.TextAsync(), JsonOptions);
                SubmissionSummaryDto? match = submissions?.FirstOrDefault(s => s.TitleSnapshot == title);
                if (match is not { Status: "Approved", AssignedSku: not null })
                {
                    return false;
                }

                assignedSku = match.AssignedSku;
                return true;
            },
            cancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(assignedSku));

        // Proves SubmissionResultConsumer's approval-time broadcast to
        // InventoryNotificationBroadcaster (today's fix) reaches an already-open
        // Inventory tab - inventoryPage was opened before this SKU existed and is
        // never reloaded.
        ILocator inventoryRow = inventoryPage.Locator("tr", new PageLocatorOptions { HasText = title });
        await Expect(inventoryRow).ToBeVisibleAsync();
        await Expect(inventoryRow).ToContainTextAsync(assignedSku);

        // submissionsPage's own row for this draft also updates to "Approved" live,
        // with no reload of its own since the page was first opened.
        await Expect(submissionsRow).ToContainTextAsync("Approved");

        // editObserverPage was opened before approval and is never reloaded - this
        // draft has a cover image, so approval first moves it to
        // PendingImageCleanup, and only SubmissionImageDeletionConsumer's own
        // broadcast (once the blob is gone and the Draft row is removed) turns the
        // page's refetch into a 404, showing this panel.
        await Expect(editObserverPage.GetByText("Approved", new PageGetByTextOptions { Exact = true }))
            .ToBeVisibleAsync();
    }

    private sealed record SubmissionSummaryDto(string TitleSnapshot, string Status, string? AssignedSku);
}
