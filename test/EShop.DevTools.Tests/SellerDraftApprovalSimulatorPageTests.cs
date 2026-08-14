// <copyright file="SellerDraftApprovalSimulatorPageTests.cs" company="Henrik Jensen">
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

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Messaging.ServiceBus;
using Hj.EShop.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Hj.EShop.DevTools.Tests;

// Uses the real Razor Pages antiforgery pipeline (no custom bypass), so these tests
// also cover that POSTing without a valid token still fails the same way it would in
// the real tool.
public sealed partial class SellerDraftApprovalSimulatorPageTests
{
    private static readonly Uri PageUri = new("/Tools/SellerDraftApprovalSimulator", UriKind.Relative);

    [Fact]
    public async Task GetToolPage_ListsWhateverIsInThePendingSubmissionStore()
    {
        using DevToolsWebApplicationFactory factory = new();
        SubmissionRequestMessage message = CreateMessage();
        factory.Store.Add(message);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(PageUri, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(message.TitleSnapshot, html, StringComparison.Ordinal);
        Assert.Contains(message.FormatVariants![0].Format, html, StringComparison.Ordinal);

        // Format variants and Price share one "Details" column, not separate columns.
        Assert.Contains("<th>Details</th>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Format variants</th>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<th>Price</th>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetToolPage_MerchandiseSubmission_ListsKindAndShowsPriceInDetails()
    {
        using DevToolsWebApplicationFactory factory = new();
        SubmissionRequestMessage message = CreateMerchandiseMessage();
        factory.Store.Add(message);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(PageUri, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(message.TitleSnapshot, html, StringComparison.Ordinal);
        Assert.Contains(nameof(SubmissionKind.Merchandise), html, StringComparison.Ordinal);
        // Checks the numeric amount only, not the full currency-formatted string - the
        // currency symbol's HTML encoding (e.g. "&#xA4;" for the invariant culture's
        // "¤") depends on the runtime's culture, which this test shouldn't couple to.
        Assert.Contains(message.Price!.Value.ToString("0.00", CultureInfo.InvariantCulture), html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetToolPage_SubmissionWithImages_RendersThumbnailLinkForEachImage()
    {
        using DevToolsWebApplicationFactory factory = new();
        SubmissionRequestMessage message = CreateMessage() with { ImageBlobReferences = ["drafts/123/cover.jpg"] };
        factory.Store.Add(message);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(PageUri, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("<th>Images</th>", html, StringComparison.Ordinal);
        string expectedUrl = "?handler=Image&amp;blobReference=" + Uri.EscapeDataString(message.ImageBlobReferences[0]);
        Assert.Contains($"href=\"{expectedUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains($"src=\"{expectedUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetToolPage_SubmissionWithNoImages_RendersEmptyImagesCell()
    {
        using DevToolsWebApplicationFactory factory = new();
        SubmissionRequestMessage message = CreateMerchandiseMessage();
        factory.Store.Add(message);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(PageUri, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetImage_KnownBlobReferenceOfAPendingSubmission_StreamsTheBlobContent()
    {
        using DevToolsWebApplicationFactory factory = new();
        const string blobReference = "drafts/123/cover.jpg";
        SubmissionRequestMessage message = CreateMessage() with { ImageBlobReferences = [blobReference] };
        factory.Store.Add(message);
        byte[] imageBytes = [1, 2, 3, 4];
        factory.BlobContainerClient.Blobs[blobReference] = (imageBytes, "image/jpeg");
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/Tools/SellerDraftApprovalSimulator?handler=Image&blobReference={Uri.EscapeDataString(blobReference)}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        byte[] body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(imageBytes, body);
    }

    [Fact]
    public async Task GetImage_BlobReferenceNotBelongingToAnyPendingSubmission_ReturnsNotFound()
    {
        using DevToolsWebApplicationFactory factory = new();
        factory.Store.Add(CreateMessage() with { ImageBlobReferences = ["drafts/123/cover.jpg"] });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/Tools/SellerDraftApprovalSimulator?handler=Image&blobReference=some/other/blob.jpg", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostApprove_PublishesApprovedResultWithAssignedSkuAndRemovesFromStore()
    {
        using DevToolsWebApplicationFactory factory = new();
        SubmissionRequestMessage message = CreateMessage();
        factory.Store.Add(message);
        HttpClient client = CreateClient(factory);
        string token = await GetAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["submissionId"] = message.SubmissionId.ToString(),
            ["__RequestVerificationToken"] = token,
        });
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/Tools/SellerDraftApprovalSimulator?handler=Approve", UriKind.Relative), form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        ServiceBusMessage sentMessage = Assert.Single(factory.ServiceBusClient.SentMessages);
        SubmissionResultMessage result = JsonSerializer.Deserialize<SubmissionResultMessage>(sentMessage.Body.ToString())!;
        Assert.Equal(message.SubmissionId, result.SubmissionId);
        Assert.True(result.Approved);
        Assert.NotNull(result.AssignedSku);
        Assert.Null(result.RejectionReason);
        Assert.False(factory.Store.TryGet(message.SubmissionId, out _));
    }

    [Fact]
    public async Task PostReject_PublishesRejectedResultWithReasonAndRemovesFromStore()
    {
        using DevToolsWebApplicationFactory factory = new();
        SubmissionRequestMessage message = CreateMessage();
        factory.Store.Add(message);
        HttpClient client = CreateClient(factory);
        string token = await GetAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["submissionId"] = message.SubmissionId.ToString(),
            ["rejectionReason"] = "Needs a better description.",
            ["__RequestVerificationToken"] = token,
        });
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/Tools/SellerDraftApprovalSimulator?handler=Reject", UriKind.Relative), form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        ServiceBusMessage sentMessage = Assert.Single(factory.ServiceBusClient.SentMessages);
        SubmissionResultMessage result = JsonSerializer.Deserialize<SubmissionResultMessage>(sentMessage.Body.ToString())!;
        Assert.Equal(message.SubmissionId, result.SubmissionId);
        Assert.False(result.Approved);
        Assert.Null(result.AssignedSku);
        Assert.Equal("Needs a better description.", result.RejectionReason);
        Assert.False(factory.Store.TryGet(message.SubmissionId, out _));
    }

    [Fact]
    public async Task PostApprove_TwoSubmissionsApprovedBackToBack_GetDifferentSkus()
    {
        // Back-to-back approvals must still get different SKUs within the same UUIDv7
        // timestamp window.
        using DevToolsWebApplicationFactory factory = new();
        SubmissionRequestMessage first = CreateMessage();
        SubmissionRequestMessage second = CreateMessage() with { SubmissionId = Guid.CreateVersion7() };
        factory.Store.Add(first);
        factory.Store.Add(second);
        HttpClient client = CreateClient(factory);

        async Task<string> ApproveAsync(Guid submissionId)
        {
            string token = await GetAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
            FormUrlEncodedContent form = new(new Dictionary<string, string>
            {
                ["submissionId"] = submissionId.ToString(),
                ["__RequestVerificationToken"] = token,
            });
            HttpResponseMessage response = await client.PostAsync(
                new Uri("/Tools/SellerDraftApprovalSimulator?handler=Approve", UriKind.Relative), form, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return submissionId.ToString();
        }

        await ApproveAsync(first.SubmissionId);
        await ApproveAsync(second.SubmissionId);

        Assert.Equal(2, factory.ServiceBusClient.SentMessages.Count);
        List<string?> skus = [.. factory.ServiceBusClient.SentMessages
            .Select(m => JsonSerializer.Deserialize<SubmissionResultMessage>(m.Body.ToString())!.AssignedSku)];
        Assert.All(skus, sku => Assert.Matches("^SKU-[0-9A-F]{8}$", sku!));
        Assert.NotEqual(skus[0], skus[1]);
    }

    internal static HttpClient CreateClient(DevToolsWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
    }

    internal static SubmissionRequestMessage CreateMessage()
    {
        return new SubmissionRequestMessage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), SubmissionKind.Movie, "A Movie", "A description.", "Drama", 2026,
            [new SubmissionFormatVariantPayload("Dvd", 9.99m)], AssociatedMovieTitle: null, ImageBlobReferences: ["blob-reference"],
            Price: null);
    }

    private static SubmissionRequestMessage CreateMerchandiseMessage()
    {
        return new SubmissionRequestMessage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), SubmissionKind.Merchandise, "A T-Shirt", "A description.",
            Genre: null, YearOfRelease: null, FormatVariants: null, AssociatedMovieTitle: "A Movie", ImageBlobReferences: [],
            Price: 19.99m);
    }

    internal static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.GetAsync(PageUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        string html = await response.Content.ReadAsStringAsync(cancellationToken);
        Match match = AntiforgeryTokenRegex().Match(html);

        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException("The tool page did not render an antiforgery token.");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
