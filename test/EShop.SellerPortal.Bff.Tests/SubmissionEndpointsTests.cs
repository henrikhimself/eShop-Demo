// <copyright file="SubmissionEndpointsTests.cs" company="Henrik Jensen">
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

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Azure.Messaging.ServiceBus;
using Hj.EShop.Messaging;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

public sealed class SubmissionEndpointsTests : IAsyncLifetime
{
    private readonly SellerPortalWebApplicationFactory factory = new();

    public async ValueTask InitializeAsync()
    {
        await factory.EnsureDatabaseCreatedAsync();
    }

    public ValueTask DisposeAsync()
    {
        factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task PostSubmit_MissingCoverImage_ReturnsBadRequest()
    {
        (HttpClient client, Guid draftId) = await CreateDraftWithFormatVariantAsync("seller-submit-noimage");

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSubmit_MissingFormatVariant_ReturnsBadRequest()
    {
        (HttpClient client, Guid draftId) = await CreateDraftWithImageAsync("seller-submit-novariant");

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSubmit_ValidDraft_MarksPendingReviewAndPublishesSubmissionRequest()
    {
        (HttpClient client, Guid draftId) = await CreateSubmittableDraftAsync("seller-submit-ok");

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        SubmissionSummary? summary = await response.Content.ReadFromJsonAsync<SubmissionSummary>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(summary);
        Assert.Equal(SubmissionStatus.Pending, summary!.Status);

        ServiceBusMessage sentMessage = Assert.Single(factory.ServiceBusClient.SentMessages);
        SubmissionRequestMessage message = JsonSerializer.Deserialize<SubmissionRequestMessage>(sentMessage.Body.ToString())!;
        Assert.Equal(summary.Id, message.SubmissionId);
        Assert.Equal(SubmissionKind.Movie, message.Kind);
        Assert.NotNull(message.FormatVariants);
        Assert.Single(message.FormatVariants);
        Assert.Single(message.ImageBlobReferences);
        Assert.Null(message.AssociatedMovieTitle);

        // The draft is no longer actionable via the movie draft endpoint once submitted.
        HttpResponseMessage getDraftResponse = await client.GetAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        MovieDraftDetail draft = (await getDraftResponse.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;
        Assert.Equal(DraftStatus.PendingReview, draft.Status);
    }

    [Fact]
    public async Task PostSubmit_BroadcastsPendingSummaryOverSse()
    {
        const string subjectId = "seller-submit-broadcast";
        (HttpClient client, Guid draftId) = await CreateSubmittableDraftAsync(subjectId);

        SubmissionNotificationBroadcaster broadcaster = factory.Services.GetRequiredService<SubmissionNotificationBroadcaster>();
        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();
        Seller seller = await db.Sellers.SingleAsync(s => s.SubjectId == subjectId, TestContext.Current.CancellationToken);
        Channel<SubmissionSummary> channel = broadcaster.Subscribe(seller.Id);

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        SubmissionSummary broadcastSummary = await channel.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(SubmissionStatus.Pending, broadcastSummary.Status);

        broadcaster.Unsubscribe(seller.Id, channel);
    }

    [Fact]
    public async Task PostSubmit_AlreadyPendingReview_Returns409()
    {
        (HttpClient client, Guid draftId) = await CreateSubmittableDraftAsync("seller-submit-twice");
        HttpResponseMessage firstSubmit = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        firstSubmit.EnsureSuccessStatusCode();

        HttpResponseMessage secondSubmit = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, secondSubmit.StatusCode);
    }

    [Fact]
    public async Task PostSubmit_PublishFailure_ReturnsInternalServerErrorButKeepsCommittedState()
    {
        (HttpClient client, Guid draftId) = await CreateSubmittableDraftAsync("seller-submit-publishfail");
        factory.ServiceBusClient.ThrowOnSend = true;
        factory.ServiceBusClient.ExceptionToThrowOnSend = new ServiceBusException("Simulated broker outage.", ServiceBusFailureReason.GeneralError);

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(factory.ServiceBusClient.SentMessages);

        HttpResponseMessage getDraftResponse = await client.GetAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        MovieDraftDetail draft = (await getDraftResponse.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;
        Assert.Equal(DraftStatus.PendingReview, draft.Status);
    }

    [Fact]
    public async Task PostSubmit_TransientPublishFailure_RetriesAndSucceeds()
    {
        // Simulates a Service Bus namespace that is briefly unreachable: the first two
        // attempts fail, and SendWithRetryAsync's bounded retry (MaxRetryAttempts: 2, so
        // 3 attempts total) absorbs both failures without the Seller seeing a 500.
        (HttpClient client, Guid draftId) = await CreateSubmittableDraftAsync("seller-submit-transientfail");
        factory.ServiceBusClient.FailuresBeforeSuccess = 2;
        factory.ServiceBusClient.ExceptionToThrowOnSend = new ServiceBusException("Simulated transient broker unavailability.", ServiceBusFailureReason.ServiceBusy);

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(factory.ServiceBusClient.SentMessages);

        HttpResponseMessage getDraftResponse = await client.GetAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        MovieDraftDetail draft = (await getDraftResponse.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;
        Assert.Equal(DraftStatus.PendingReview, draft.Status);
    }

    [Fact]
    public async Task PostSubmit_PublishFailureIsNotAServiceBusException_StillReturnsInternalServerErrorButKeepsCommittedState()
    {
        // Any exception from the transport (a socket error, a credential failure, a
        // send timeout), not just ServiceBusException, must hit the deliberate
        // log-and-500 path instead of escaping as an unhandled exception.
        (HttpClient client, Guid draftId) = await CreateSubmittableDraftAsync("seller-submit-publishfail-non-sb");
        factory.ServiceBusClient.ThrowOnSend = true;
        factory.ServiceBusClient.ExceptionToThrowOnSend = new IOException("Simulated transport-level failure.");

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(factory.ServiceBusClient.SentMessages);

        HttpResponseMessage getDraftResponse = await client.GetAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        MovieDraftDetail draft = (await getDraftResponse.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;
        Assert.Equal(DraftStatus.PendingReview, draft.Status);
    }

    [Fact]
    public async Task PostMerchandiseSubmit_ZeroImages_MarksPendingReviewAndPublishesSubmissionRequest()
    {
        (HttpClient client, Guid draftId) = await CreateMerchandiseDraftAsync("seller-merch-submit-noimages");
        (await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}", UriKind.Relative),
            new UpdateMerchandiseDraftRequest("A Mug", "A description.", 14.99m, AssociatedMovieTitle: null),
            TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        SubmissionSummary? summary = await response.Content.ReadFromJsonAsync<SubmissionSummary>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(summary);
        Assert.Equal(SubmissionStatus.Pending, summary!.Status);

        ServiceBusMessage sentMessage = Assert.Single(factory.ServiceBusClient.SentMessages);
        SubmissionRequestMessage message = JsonSerializer.Deserialize<SubmissionRequestMessage>(sentMessage.Body.ToString())!;
        Assert.Equal(summary.Id, message.SubmissionId);
        Assert.Equal(SubmissionKind.Merchandise, message.Kind);
        Assert.Null(message.FormatVariants);
        Assert.Null(message.Genre);
        Assert.Null(message.YearOfRelease);
        Assert.Equal(14.99m, message.Price);
        Assert.Empty(message.ImageBlobReferences);

        HttpResponseMessage getDraftResponse = await client.GetAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        MerchandiseDraftDetail draft = (await getDraftResponse.Content.ReadFromJsonAsync<MerchandiseDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;
        Assert.Equal(DraftStatus.PendingReview, draft.Status);
    }

    [Fact]
    public async Task PostMerchandiseSubmit_BroadcastsPendingSummaryOverSse()
    {
        const string subjectId = "seller-merch-submit-broadcast";
        (HttpClient client, Guid draftId) = await CreateMerchandiseDraftAsync(subjectId);

        SubmissionNotificationBroadcaster broadcaster = factory.Services.GetRequiredService<SubmissionNotificationBroadcaster>();
        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();
        Seller seller = await db.Sellers.SingleAsync(s => s.SubjectId == subjectId, TestContext.Current.CancellationToken);
        Channel<SubmissionSummary> channel = broadcaster.Subscribe(seller.Id);

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        SubmissionSummary broadcastSummary = await channel.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(SubmissionStatus.Pending, broadcastSummary.Status);

        broadcaster.Unsubscribe(seller.Id, channel);
    }

    [Fact]
    public async Task PostMerchandiseSubmit_AlreadyPendingReview_Returns409()
    {
        (HttpClient client, Guid draftId) = await CreateMerchandiseDraftAsync("seller-merch-submit-twice");
        HttpResponseMessage firstSubmit = await client.PostAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        firstSubmit.EnsureSuccessStatusCode();

        HttpResponseMessage secondSubmit = await client.PostAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, secondSubmit.StatusCode);
    }

    [Fact]
    public async Task PostCancelReview_PendingReviewMovieDraft_RevertsToDraftAndCancelsSubmission()
    {
        (HttpClient client, Guid draftId) = await CreateSubmittableDraftAsync("seller-cancel-movie");
        (await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/cancel-review", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        HttpResponseMessage getDraftResponse = await client.GetAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        MovieDraftDetail draft = (await getDraftResponse.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;
        Assert.Equal(DraftStatus.Draft, draft.Status);

        HttpResponseMessage getSubmissionsResponse = await client.GetAsync(
            new Uri("/bff/api/submissions", UriKind.Relative), TestContext.Current.CancellationToken);
        List<SubmissionSummary>? submissions = await getSubmissionsResponse.Content.ReadFromJsonAsync<List<SubmissionSummary>>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        SubmissionSummary submission = Assert.Single(submissions!);
        Assert.Equal(SubmissionStatus.Cancelled, submission.Status);
        Assert.NotNull(submission.RespondedAtUtc);

        // The submit above already published a SubmissionRequestMessage - the
        // cancellation is the second, distinct message. SubmissionCancelledMessage's
        // single property distinguishes it from SubmissionRequestMessage (which also
        // has a leading SubmissionId property) since both would otherwise
        // successfully deserialize into either shape.
        ServiceBusMessage cancellationMessage = Assert.Single(
            factory.ServiceBusClient.SentMessages,
            m => JsonDocument.Parse(m.Body).RootElement.EnumerateObject().Count() == 1);
        SubmissionCancelledMessage cancelledMessage = JsonSerializer.Deserialize<SubmissionCancelledMessage>(cancellationMessage.Body.ToString())!;
        Assert.Equal(submission.Id, cancelledMessage.SubmissionId);

        // The draft is editable and resubmittable again after cancelling.
        HttpResponseMessage resubmitResponse = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, resubmitResponse.StatusCode);
    }

    [Fact]
    public async Task PostCancelReview_MerchandiseDraft_RevertsToDraftAndCancelsSubmission()
    {
        (HttpClient client, Guid draftId) = await CreateMerchandiseDraftAsync("seller-cancel-merch");
        (await client.PostAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}/cancel-review", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        HttpResponseMessage getDraftResponse = await client.GetAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        MerchandiseDraftDetail draft = (await getDraftResponse.Content.ReadFromJsonAsync<MerchandiseDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;
        Assert.Equal(DraftStatus.Draft, draft.Status);
    }

    [Fact]
    public async Task PostCancelReview_DraftNotPendingReview_Returns409()
    {
        (HttpClient client, Guid draftId) = await CreateSubmittableDraftAsync("seller-cancel-not-pending");

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/cancel-review", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostCancelReview_UnknownDraft_Returns404()
    {
        HttpClient client = CreateAuthenticatedClient("seller-cancel-unknown");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{Guid.CreateVersion7()}/cancel-review", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostCancelReview_AnotherSellersDraft_Returns404()
    {
        (HttpClient ownerClient, Guid draftId) = await CreateSubmittableDraftAsync("seller-cancel-owner");
        (await ownerClient.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        HttpClient otherClient = CreateAuthenticatedClient("seller-cancel-other");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(otherClient, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await otherClient.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/cancel-review", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostCancelReview_BroadcastsCancelledSummaryOverSse()
    {
        (HttpClient client, Guid draftId) = await CreateSubmittableDraftAsync("seller-cancel-broadcast");
        (await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        SubmissionNotificationBroadcaster broadcaster = factory.Services.GetRequiredService<SubmissionNotificationBroadcaster>();
        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();
        Seller seller = await db.Sellers.SingleAsync(s => s.SubjectId == "seller-cancel-broadcast", TestContext.Current.CancellationToken);
        Channel<SubmissionSummary> channel = broadcaster.Subscribe(seller.Id);

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/cancel-review", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        SubmissionSummary broadcastSummary = await channel.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(SubmissionStatus.Cancelled, broadcastSummary.Status);

        broadcaster.Unsubscribe(seller.Id, channel);
    }

    [Fact]
    public async Task GetSubmissions_ReturnsSellerSubmissionsNewestFirst()
    {
        (HttpClient client, Guid firstDraftId) = await CreateSubmittableDraftAsync("seller-submissions-list");
        (await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{firstDraftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        Guid secondDraftId = await CreateSubmittableDraftUsingExistingClientAsync(client);
        (await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{secondDraftId}/submit", UriKind.Relative), content: null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/submissions", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        List<SubmissionSummary>? submissions = await response.Content.ReadFromJsonAsync<List<SubmissionSummary>>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(submissions);
        Assert.Equal(2, submissions!.Count);
        Assert.True(submissions[0].SubmittedAtUtc >= submissions[1].SubmittedAtUtc);
    }

    private async Task<(HttpClient Client, Guid DraftId)> CreateSubmittableDraftAsync(string subjectId)
    {
        (HttpClient client, Guid draftId) = await CreateDraftWithImageAsync(subjectId);
        await AddFormatVariantAsync(client, draftId);

        return (client, draftId);
    }

    private static async Task<Guid> CreateSubmittableDraftUsingExistingClientAsync(HttpClient client)
    {
        Guid draftId = await CreateDraftAsync(client);
        await UploadImageAsync(client, draftId);
        await AddFormatVariantAsync(client, draftId);

        return draftId;
    }

    private async Task<(HttpClient Client, Guid DraftId)> CreateDraftWithImageAsync(string subjectId)
    {
        HttpClient client = CreateAuthenticatedClient(subjectId);
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        Guid draftId = await CreateDraftAsync(client);
        await UploadImageAsync(client, draftId);

        return (client, draftId);
    }

    private async Task<(HttpClient Client, Guid DraftId)> CreateDraftWithFormatVariantAsync(string subjectId)
    {
        HttpClient client = CreateAuthenticatedClient(subjectId);
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        Guid draftId = await CreateDraftAsync(client);
        await AddFormatVariantAsync(client, draftId);

        return (client, draftId);
    }

    private async Task<(HttpClient Client, Guid DraftId)> CreateMerchandiseDraftAsync(string subjectId)
    {
        HttpClient client = CreateAuthenticatedClient(subjectId);
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PostAsync(
            new Uri("/bff/api/drafts/merchandise", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        MerchandiseDraftDetail detail = (await response.Content.ReadFromJsonAsync<MerchandiseDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;

        return (client, detail.Id);
    }

    private HttpClient CreateAuthenticatedClient(string subjectId)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, subjectId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");
        return client;
    }

    private static async Task<Guid> CreateDraftAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/bff/api/drafts/movies", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        MovieDraftDetail detail = (await response.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;

        return detail.Id;
    }

    private static async Task UploadImageAsync(HttpClient client, Guid draftId)
    {
        using MultipartFormDataContent form = new();
        using ByteArrayContent fileContent = new([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "cover.jpg");

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/images", UriKind.Relative), form, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task AddFormatVariantAsync(HttpClient client, Guid draftId)
    {
        UpdateMovieDraftRequest request = new(
            "A Movie",
            "Drama",
            "A description.",
            2026,
            [new UpdateFormatVariantRequest(null, MovieFormat.Dvd, 9.99m)]);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative), request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
