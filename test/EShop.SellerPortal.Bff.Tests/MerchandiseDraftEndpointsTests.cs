// <copyright file="MerchandiseDraftEndpointsTests.cs" company="Henrik Jensen">
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
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

public sealed class MerchandiseDraftEndpointsTests : IAsyncLifetime
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
    public async Task PostMerchandiseDraft_CreatesEmptyDraftInDraftStatus()
    {
        using HttpClient client = CreateSellerClient("seller-merch-create");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PostAsync(
            new Uri("/bff/api/drafts/merchandise", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        MerchandiseDraftDetail? detail = await response.Content.ReadFromJsonAsync<MerchandiseDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(detail);
        Assert.Equal(DraftStatus.Draft, detail!.Status);
        Assert.Equal(string.Empty, detail.ProductName);
        Assert.Null(detail.AssociatedMovieTitle);
        Assert.Empty(detail.Images);
    }

    [Fact]
    public async Task PutMerchandiseDraft_InDraftStatus_UpdatesFields()
    {
        using HttpClient client = CreateSellerClient("seller-merch-update");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        MerchandiseDraftDetail created = await CreateMerchandiseDraftAsync(client, TestContext.Current.CancellationToken);

        UpdateMerchandiseDraftRequest request = new("A Mug", "Updated description", 12.5m, "A Movie");

        HttpResponseMessage response = await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/merchandise/{created.Id}", UriKind.Relative), request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        MerchandiseDraftDetail? updated = await response.Content.ReadFromJsonAsync<MerchandiseDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("A Mug", updated!.ProductName);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal(12.5m, updated.Price);
        Assert.Equal("A Movie", updated.AssociatedMovieTitle);
    }

    [Fact]
    public async Task PutMerchandiseDraft_WhilePendingReview_Returns409()
    {
        Guid draftId = await SeedMerchandiseDraftAsync("seller-merch-conflict-put", DraftStatus.PendingReview, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient("seller-merch-conflict-put");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}", UriKind.Relative),
            new UpdateMerchandiseDraftRequest("Name", "Description", 0m, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("", "D", 1)]
    [InlineData("Name", "D", -1)]
    public async Task PutMerchandiseDraft_WithInvalidField_Returns400(string productName, string description, decimal price)
    {
        using HttpClient client = CreateSellerClient($"seller-merch-invalid-{productName}{price}");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        MerchandiseDraftDetail created = await CreateMerchandiseDraftAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            new Uri($"/bff/api/drafts/merchandise/{created.Id}", UriKind.Relative),
            new UpdateMerchandiseDraftRequest(productName, description, price, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMerchandiseDraft_WhilePendingReview_Returns409()
    {
        Guid draftId = await SeedMerchandiseDraftAsync("seller-merch-conflict-delete", DraftStatus.PendingReview, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient("seller-merch-conflict-delete");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.DeleteAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMerchandiseDraft_InDraftStatus_RemovesIt()
    {
        Guid draftId = await SeedMerchandiseDraftAsync("seller-merch-delete", DraftStatus.Draft, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient("seller-merch-delete");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage deleteResponse = await client.DeleteAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage getResponse = await client.GetAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteMerchandiseDraft_WithImages_QueuesTheirBlobDeletion()
    {
        Guid draftId = await SeedMerchandiseDraftAsync("seller-merch-delete-with-images", DraftStatus.Draft, TestContext.Current.CancellationToken);
        Guid firstImageId = await SeedMerchandiseDraftImageAsync(draftId, "blob-1", 0, TestContext.Current.CancellationToken);
        Guid secondImageId = await SeedMerchandiseDraftImageAsync(draftId, "blob-2", 1, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient("seller-merch-delete-with-images");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage deleteResponse = await client.DeleteAsync(
            new Uri($"/bff/api/drafts/merchandise/{draftId}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // The Draft row is gone immediately (durable intent), but the actual blob
        // delete is deferred to SubmissionImageDeletionConsumer via the same
        // queued-message path as the single-image delete route.
        List<BlobDeletionMessage> deletionMessages =
        [
            .. factory.ServiceBusClient.SentMessages.Select(m => JsonSerializer.Deserialize<BlobDeletionMessage>(m.Body.ToString())!),
        ];
        Assert.Equal(2, deletionMessages.Count);
        Assert.Contains(deletionMessages, m => m.DraftId == draftId && m.DraftImageId == firstImageId && m.BlobReference == "blob-1");
        Assert.Contains(deletionMessages, m => m.DraftId == draftId && m.DraftImageId == secondImageId && m.BlobReference == "blob-2");
    }

    [Fact]
    public async Task GetMerchandiseDraft_BelongingToAnotherSeller_Returns404()
    {
        Guid otherSellersDraftId = await SeedMerchandiseDraftAsync("seller-merch-owner", DraftStatus.Draft, TestContext.Current.CancellationToken);
        using HttpClient client = CreateSellerClient("seller-merch-intruder");

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/bff/api/drafts/merchandise/{otherSellersDraftId}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateSellerClient(string subjectId)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, subjectId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");
        return client;
    }

    private static async Task<MerchandiseDraftDetail> CreateMerchandiseDraftAsync(HttpClient client, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/bff/api/drafts/merchandise", UriKind.Relative), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MerchandiseDraftDetail>(TestJsonOptions.Default, cancellationToken))!;
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

    private async Task<Guid> SeedMerchandiseDraftImageAsync(Guid draftId, string blobReference, int position, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();

        DraftImage image = new() { Id = Guid.CreateVersion7(), DraftId = draftId, BlobReference = blobReference, Position = position };
        db.Add(image);
        await db.SaveChangesAsync(cancellationToken);

        return image.Id;
    }
}
