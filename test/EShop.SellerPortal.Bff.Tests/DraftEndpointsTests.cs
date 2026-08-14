// <copyright file="DraftEndpointsTests.cs" company="Henrik Jensen">
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
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

public sealed class DraftEndpointsTests : IAsyncLifetime
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
    public async Task GetUser_Unauthenticated_Returns401()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/user", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDrafts_AuthenticatedWithoutSellerRole_Returns403()
    {
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, "unapproved-subject");

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/drafts", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDrafts_AuthenticatedWithSellerRole_ProvisionsSellerJustInTimeAndReturnsEmptyList()
    {
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, "new-seller-subject");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/drafts", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("[]", body);
    }

    [Fact]
    public async Task GetDrafts_ExcludesDraftsPendingImageCleanup()
    {
        const string subjectId = "seller-list-filter";
        Guid visibleDraftId = await SeedMovieDraftAsync(subjectId, DraftStatus.Draft, TestContext.Current.CancellationToken);
        await SeedMovieDraftAsync(subjectId, DraftStatus.PendingImageCleanup, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient(subjectId);

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/drafts", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        List<DraftSummary>? drafts = await response.Content.ReadFromJsonAsync<List<DraftSummary>>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(drafts);
        Assert.Equal([visibleDraftId], drafts!.Select(d => d.Id));
    }

    [Fact]
    public async Task GetDrafts_ReportsKindForEachDraftType()
    {
        const string subjectId = "seller-list-kinds";
        Guid movieDraftId = await SeedMovieDraftAsync(subjectId, DraftStatus.Draft, TestContext.Current.CancellationToken);
        Guid merchandiseDraftId = await SeedMerchandiseDraftAsync(subjectId, DraftStatus.Draft, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient(subjectId);

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/drafts", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        List<DraftSummary>? drafts = await response.Content.ReadFromJsonAsync<List<DraftSummary>>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(drafts);
        Assert.Equal(DraftKind.Movie, drafts!.Single(d => d.Id == movieDraftId).Kind);
        Assert.Equal(DraftKind.Merchandise, drafts.Single(d => d.Id == merchandiseDraftId).Kind);
    }

    [Fact]
    public async Task PostMovieDraft_CreatesEmptyDraftInDraftStatus()
    {
        using HttpClient client = CreateSellerClient("seller-create");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PostAsync(
            new Uri("/bff/api/drafts/movies", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        MovieDraftDetail? detail = await response.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(detail);
        Assert.Equal(DraftStatus.Draft, detail!.Status);
        Assert.Empty(detail.FormatVariants);
        Assert.Null(detail.CoverImage);
    }

    [Fact]
    public async Task PutMovieDraft_InDraftStatus_UpdatesFieldsAndReconcilesFormatVariants()
    {
        using HttpClient client = CreateSellerClient("seller-update");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        MovieDraftDetail created = await CreateMovieDraftAsync(client, TestContext.Current.CancellationToken);

        UpdateMovieDraftRequest request = new(
            "Updated Title",
            "Sci-Fi",
            "Updated description",
            2027,
            [new UpdateFormatVariantRequest(null, MovieFormat.BluRay, 19.99m)]);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/movies/{created.Id}", UriKind.Relative), request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        MovieDraftDetail? updated = await response.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated!.Title);
        Assert.Equal(2027, updated.YearOfRelease);
        Assert.Equal([MovieFormat.BluRay], updated.FormatVariants.Select(v => v.Format));
    }

    [Fact]
    public async Task PutMovieDraft_WithMissingFormatVariants_Returns400()
    {
        using HttpClient client = CreateSellerClient("seller-null-variants");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        MovieDraftDetail created = await CreateMovieDraftAsync(client, TestContext.Current.CancellationToken);

        // Deliberately omits formatVariants - System.Text.Json binds a missing member
        // as null regardless of the record's non-nullable annotation.
        using StringContent body = new(
            """{"title":"T","genre":"G","description":"D","yearOfRelease":2026}""",
            System.Text.Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await client.PutAsync(
            new Uri($"/bff/api/drafts/movies/{created.Id}", UriKind.Relative), body, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMovieDraft_WithUnknownFormatVariantId_Returns404AndKeepsExistingVariants()
    {
        using HttpClient client = CreateSellerClient("seller-unknown-variant");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        MovieDraftDetail created = await CreateMovieDraftAsync(client, TestContext.Current.CancellationToken);
        await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/movies/{created.Id}", UriKind.Relative),
            new UpdateMovieDraftRequest("T", "G", "D", 2026, [new UpdateFormatVariantRequest(null, MovieFormat.Dvd, 9.99m)]),
            TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/movies/{created.Id}", UriKind.Relative),
            new UpdateMovieDraftRequest("T", "G", "D", 2026, [new UpdateFormatVariantRequest(Guid.CreateVersion7(), MovieFormat.BluRay, 19.99m)]),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        HttpResponseMessage getResponse = await client.GetAsync(
            new Uri($"/bff/api/drafts/movies/{created.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        MovieDraftDetail? unchanged = await getResponse.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(unchanged);
        Assert.Equal([MovieFormat.Dvd], unchanged!.FormatVariants.Select(v => v.Format));
    }

    [Theory]
    [InlineData("", "G", "D", 2026)]
    [InlineData("T", "", "D", 2026)]
    [InlineData("T", "G", "D", 1800)]
    [InlineData("T", "G", "D", 3000)]
    public async Task PutMovieDraft_WithInvalidField_Returns400(string title, string genre, string description, int year)
    {
        using HttpClient client = CreateSellerClient($"seller-invalid-{title}{genre}{year}");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        MovieDraftDetail created = await CreateMovieDraftAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/movies/{created.Id}", UriKind.Relative),
            new UpdateMovieDraftRequest(title, genre, description, year, []),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMovieDraft_WithNegativeFormatVariantPrice_Returns400()
    {
        using HttpClient client = CreateSellerClient("seller-negative-price");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        MovieDraftDetail created = await CreateMovieDraftAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/movies/{created.Id}", UriKind.Relative),
            new UpdateMovieDraftRequest("T", "G", "D", 2026, [new UpdateFormatVariantRequest(null, MovieFormat.Dvd, -1m)]),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMovieDraft_WhilePendingReview_Returns409()
    {
        Guid draftId = await SeedMovieDraftAsync("seller-conflict-put", DraftStatus.PendingReview, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient("seller-conflict-put");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative),
            new UpdateMovieDraftRequest("T", "G", "D", 2026, []),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMovieDraft_WhilePendingReview_Returns409()
    {
        Guid draftId = await SeedMovieDraftAsync("seller-conflict-delete", DraftStatus.PendingReview, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient("seller-conflict-delete");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.DeleteAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMovieDraft_InDraftStatus_RemovesIt()
    {
        Guid draftId = await SeedMovieDraftAsync("seller-delete", DraftStatus.Draft, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient("seller-delete");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage deleteResponse = await client.DeleteAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage getResponse = await client.GetAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteMovieDraft_WithAnImage_QueuesItsBlobDeletion()
    {
        Guid draftId = await SeedMovieDraftAsync("seller-delete-with-image", DraftStatus.Draft, TestContext.Current.CancellationToken);
        Guid imageId = await SeedMovieDraftImageAsync(draftId, "blob-to-clean-up", TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient("seller-delete-with-image");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage deleteResponse = await client.DeleteAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // The Draft row is gone immediately (durable intent), but the actual blob
        // delete is deferred to SubmissionImageDeletionConsumer via the same
        // queued-message path as the single-image delete route.
        ServiceBusMessage sentMessage = Assert.Single(factory.ServiceBusClient.SentMessages);
        BlobDeletionMessage deletionMessage = JsonSerializer.Deserialize<BlobDeletionMessage>(sentMessage.Body.ToString())!;
        Assert.Equal(draftId, deletionMessage.DraftId);
        Assert.Equal(imageId, deletionMessage.DraftImageId);
        Assert.Equal("blob-to-clean-up", deletionMessage.BlobReference);
    }

    [Fact]
    public async Task GetMovieDraft_BelongingToAnotherSeller_Returns404()
    {
        Guid otherSellersDraftId = await SeedMovieDraftAsync("seller-owner", DraftStatus.Draft, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient("seller-intruder");

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/bff/api/drafts/movies/{otherSellersDraftId}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateSellerClient(string subjectId)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, subjectId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");
        return client;
    }

    private static async Task<MovieDraftDetail> CreateMovieDraftAsync(HttpClient client, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/bff/api/drafts/movies", UriKind.Relative), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, cancellationToken))!;
    }

    private async Task<Guid> SeedMovieDraftAsync(string subjectId, DraftStatus status, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();

        Seller seller = await db.Sellers.FirstOrDefaultAsync(s => s.SubjectId == subjectId, cancellationToken)
            ?? new Seller { Id = Guid.CreateVersion7(), SubjectId = subjectId, CreatedAtUtc = DateTimeOffset.UtcNow };
        if (db.Entry(seller).State == EntityState.Detached)
        {
            db.Sellers.Add(seller);
        }

        MovieDraft draft = new()
        {
            Id = Guid.CreateVersion7(),
            SellerId = seller.Id,
            Title = "Seed Movie",
            Genre = "Drama",
            Description = "Seed description",
            YearOfRelease = 2026,
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);

        return draft.Id;
    }

    private async Task<Guid> SeedMovieDraftImageAsync(Guid draftId, string blobReference, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();

        DraftImage image = new() { Id = Guid.CreateVersion7(), DraftId = draftId, BlobReference = blobReference, Position = 0 };
        db.Add(image);
        await db.SaveChangesAsync(cancellationToken);

        return image.Id;
    }

    private async Task<Guid> SeedMerchandiseDraftAsync(string subjectId, DraftStatus status, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();

        Seller seller = await db.Sellers.FirstOrDefaultAsync(s => s.SubjectId == subjectId, cancellationToken)
            ?? new Seller { Id = Guid.CreateVersion7(), SubjectId = subjectId, CreatedAtUtc = DateTimeOffset.UtcNow };
        if (db.Entry(seller).State == EntityState.Detached)
        {
            db.Sellers.Add(seller);
        }

        MerchandiseDraft draft = new()
        {
            Id = Guid.CreateVersion7(),
            SellerId = seller.Id,
            ProductName = "Seed Merchandise",
            Description = "Seed description",
            Price = 9.99m,
            AssociatedMovieTitle = null,
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);

        return draft.Id;
    }
}
