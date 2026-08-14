using System.Text.Json;
using System.Text.RegularExpressions;
using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Hj.EShop.AppHost.E2ETests;

// Exercises the whole Merchandise draft loop for real, mirroring
// SellerPortalMovieDraftSubmissionTests.cs - the main difference is that a merchandise
// draft can be submitted with zero images (SPEC.md sets no minimum, unlike a movie's
// required single cover image), so this test never touches the images card at all.
// Also exercises Cancel review (withdrawing a submission, editing, and resubmitting
// it) before the final approval, chosen here rather than in the Movie test since it
// has no image-cleanup complexity to interleave with. Not part of EShop.slnx - run
// via `eshop test e2e`, not `eshop test`.
public sealed class SellerPortalMerchandiseDraftSubmissionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task SubmittingAMerchandiseDraftWithNoImagesAndApprovingItInDevToolsMarksItApprovedWithASku()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string productName = $"E2E Merch {Guid.NewGuid():N}";

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

        IPage sellerPage = await session.NewPageAsync();

        // Lands directly on /drafts - AuthEndpoints.cs's /bff/login challenge redirects
        // there on success, not to the landing page.
        await E2ETestHarness.LogInAsTestSellerAsync(sellerPage, webBaseAddress);
        await sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New merchandise draft" }).ClickAsync();
        await Expect(sellerPage).ToHaveURLAsync(new Regex(@"/drafts/merchandise/[0-9a-fA-F-]+$"));

        await sellerPage.Locator("#productName").FillAsync(productName);
        await sellerPage.Locator("#description").FillAsync("An E2E test fixture merchandise item.");
        await sellerPage.Locator("#price").FillAsync("19.99");
        await sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save changes" }).ClickAsync();
        await Expect(sellerPage.Locator("h1")).ToHaveTextAsync(productName);

        // No image uploaded - confirms zero images is accepted for a merchandise
        // submission (SPEC.md sets no minimum, unlike a movie's required cover image).
        ILocator submitButton = sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Submit for review" });
        await Expect(submitButton).ToBeEnabledAsync();
        await submitButton.ClickAsync();

        // isDraftStatus flips to false once the draft leaves Draft status, disabling
        // every field - the observable signal that the submit call committed.
        await Expect(sellerPage.Locator("#productName")).ToBeDisabledAsync();

        IPage devToolsPage = await session.NewPageAsync();
        await devToolsPage.GotoAsync(devToolsBaseAddress.ToString());
        await devToolsPage.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Seller Draft Approval Simulator" })
            .First.ClickAsync();

        // The submission reaches the store asynchronously (via SellerDraftApprovalSimulatorConsumer
        // draining the queue) - poll by reloading rather than a one-shot check.
        ILocator pendingRow = devToolsPage.Locator("tr", new PageLocatorOptions { HasText = productName });
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

        // Confirms the DevTools tool page's kind-aware display didn't just survive a
        // merchandise submission but actually labeled it correctly (no format
        // variants to show, unlike a movie submission).
        await Expect(pendingRow.First).ToContainTextAsync("Merchandise");

        // Cancel review, then resubmit - proves a Seller can withdraw a submission and
        // edit/resubmit it, and that SellerSubmissionCancellationConsumer's broadcast
        // reaches this already-open DevTools tab without a reload of its own.
        await sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cancel review" }).ClickAsync();
        await Expect(sellerPage.Locator("#productName")).ToBeEnabledAsync();
        await Expect(pendingRow).Not.ToBeVisibleAsync();

        ILocator resubmitButton = sellerPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Submit for review" });
        await Expect(resubmitButton).ToBeEnabledAsync();
        await resubmitButton.ClickAsync();
        await Expect(sellerPage.Locator("#productName")).ToBeDisabledAsync();

        // The resubmission reaches the store asynchronously again - poll by reloading,
        // same as the first appearance above.
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
        await Expect(sellerPage.GetByText(new Regex($"{Regex.Escape(productName)}.*approved", RegexOptions.IgnoreCase)))
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
                SubmissionSummaryDto? match = submissions?.FirstOrDefault(s => s.TitleSnapshot == productName);
                if (match is not { Status: "Approved", AssignedSku: not null })
                {
                    return false;
                }

                assignedSku = match.AssignedSku;
                return true;
            },
            cancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(assignedSku));
    }

    private sealed record SubmissionSummaryDto(string TitleSnapshot, string Status, string? AssignedSku);
}
