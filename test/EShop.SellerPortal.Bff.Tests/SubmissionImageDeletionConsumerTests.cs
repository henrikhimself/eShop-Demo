// <copyright file="SubmissionImageDeletionConsumerTests.cs" company="Henrik Jensen">
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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading.Channels;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Hj.EShop.Messaging;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Messaging;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// See the comment on InventoryResultConsumerTests for why HandleMessageAsync is called
// directly. BlobContainerClient/BlobClient/Response all expose a protected parameterless
// constructor specifically to support this kind of lightweight test subclass, with no
// mocking library or Azurite emulator needed.
public sealed class SubmissionImageDeletionConsumerTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly SubmissionNotificationBroadcaster broadcaster = new();
    private IServiceScopeFactory scopeFactory = null!;
    private SellerPortalDbContext db = null!;

    public async ValueTask InitializeAsync()
    {
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ServiceProvider provider = new ServiceCollection()
            .AddDbContext<SellerPortalDbContext>(options => options.UseSqlite(connection))
            .BuildServiceProvider();
        scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        db = provider.GetRequiredService<SellerPortalDbContext>();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task HandleMessageAsync_LastRemainingImage_DeletesBlobAndRemovesDraft()
    {
        (MovieDraft draft, List<DraftImage> images) = await SeedDraftPendingCleanupAsync("blob-1");
        RecordingBlobContainerClient blobContainer = new();
        SubmissionImageDeletionConsumer consumer = new(
            client: null!, scopeFactory, blobContainer, broadcaster, NullLogger<SubmissionImageDeletionConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new BlobDeletionMessage(draft.Id, images[0].Id, "blob-1")));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Assert.Equal(["blob-1"], blobContainer.DeletedBlobNames);
        Assert.Empty(await db.Drafts.Where(d => d.Id == draft.Id).ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HandleMessageAsync_LastRemainingImage_BroadcastsApprovedSummaryForOpenEditPages()
    {
        (MovieDraft draft, List<DraftImage> images) = await SeedDraftPendingCleanupAsync("blob-1");
        Submission submission = await SeedApprovedSubmissionAsync(draft);
        Channel<SubmissionSummary> channel = broadcaster.Subscribe(draft.SellerId);

        RecordingBlobContainerClient blobContainer = new();
        SubmissionImageDeletionConsumer consumer = new(
            client: null!, scopeFactory, blobContainer, broadcaster, NullLogger<SubmissionImageDeletionConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new BlobDeletionMessage(draft.Id, images[0].Id, "blob-1")));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Assert.True(channel.Reader.TryRead(out SubmissionSummary? publishedSummary));
        Assert.Equal(submission.Id, publishedSummary!.Id);
        Assert.Equal(SubmissionStatus.Approved, publishedSummary.Status);
    }

    [Fact]
    public async Task HandleMessageAsync_SiblingImageRemains_DeletesOnlyThatImageAndKeepsDraft()
    {
        (MovieDraft draft, List<DraftImage> images) = await SeedDraftPendingCleanupAsync("blob-1", "blob-2");
        Channel<SubmissionSummary> channel = broadcaster.Subscribe(draft.SellerId);
        RecordingBlobContainerClient blobContainer = new();
        SubmissionImageDeletionConsumer consumer = new(
            client: null!, scopeFactory, blobContainer, broadcaster, NullLogger<SubmissionImageDeletionConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new BlobDeletionMessage(draft.Id, images[0].Id, "blob-1")));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Assert.Equal(["blob-1"], blobContainer.DeletedBlobNames);
        Draft reloaded = await db.Drafts.SingleAsync(d => d.Id == draft.Id, TestContext.Current.CancellationToken);
        Assert.Equal(["blob-2"], reloaded.Images.Select(i => i.BlobReference));

        // Not fully cleaned up yet - no open edit page needs to learn anything new.
        Assert.False(channel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task HandleMessageAsync_AlreadyCleanedUpDraft_ReturnsHandledWithoutThrowing()
    {
        (MovieDraft draft, List<DraftImage> images) = await SeedDraftPendingCleanupAsync("blob-1");
        RecordingBlobContainerClient blobContainer = new();
        SubmissionImageDeletionConsumer consumer = new(
            client: null!, scopeFactory, blobContainer, broadcaster, NullLogger<SubmissionImageDeletionConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new BlobDeletionMessage(draft.Id, images[0].Id, "blob-1")));
        await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        // Simulates a redelivered duplicate of the same message, after the first
        // delivery already removed the Draft entirely.
        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
    }

    [Fact]
    public async Task HandleMessageAsync_BlobStoreFailure_PropagatesExceptionAndLeavesDraftUntouched()
    {
        (MovieDraft draft, List<DraftImage> images) = await SeedDraftPendingCleanupAsync("blob-1");
        RecordingBlobContainerClient blobContainer = new();
        blobContainer.BlobNamesToFail.Add("blob-1");
        SubmissionImageDeletionConsumer consumer = new(
            client: null!, scopeFactory, blobContainer, broadcaster, NullLogger<SubmissionImageDeletionConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new BlobDeletionMessage(draft.Id, images[0].Id, "blob-1")));

        // Not caught/converted to a MessageHandlingResult - see
        // SubmissionImageDeletionConsumer's own comment. This propagating uncaught is
        // exactly what lets ServiceBusQueueConsumer leave the message uncompleted for
        // Service Bus's own MaxDeliveryCount-based redelivery/dead-letter (a queue-level
        // behavior this unit test cannot itself exercise without a real broker).
        await Assert.ThrowsAsync<RequestFailedException>(
            () => consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken));

        // The blob delete happens before any DB change - a failure there must leave the
        // DraftImage row (and thus the Draft) exactly as it was, for a clean retry.
        Draft reloaded = await db.Drafts.SingleAsync(d => d.Id == draft.Id, TestContext.Current.CancellationToken);
        Assert.Equal(["blob-1"], reloaded.Images.Select(i => i.BlobReference));
    }

    [Fact]
    public async Task HandleMessageAsync_DraftStillBeingEdited_KeepsTheNowImagelessDraft()
    {
        // Status stays Draft, not PendingImageCleanup: this is what a manual
        // single-image delete (DraftImageEndpoints/MerchandiseImageEndpoints) looks
        // like, as opposed to the submission-approval cleanup flow
        // (SubmissionResultConsumer) - losing its last image must not make this
        // consumer delete the still-in-progress Draft out from under the seller.
        (MovieDraft draft, List<DraftImage> images) = await SeedDraftAsync(DraftStatus.Draft, "blob-1");
        RecordingBlobContainerClient blobContainer = new();
        SubmissionImageDeletionConsumer consumer = new(
            client: null!, scopeFactory, blobContainer, broadcaster, NullLogger<SubmissionImageDeletionConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new BlobDeletionMessage(draft.Id, images[0].Id, "blob-1")));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Assert.Equal(["blob-1"], blobContainer.DeletedBlobNames);
        Draft reloaded = await db.Drafts.SingleAsync(d => d.Id == draft.Id, TestContext.Current.CancellationToken);
        Assert.Empty(reloaded.Images);
    }

    private Task<(MovieDraft Draft, List<DraftImage> Images)> SeedDraftPendingCleanupAsync(params string[] blobReferences)
    {
        return SeedDraftAsync(DraftStatus.PendingImageCleanup, blobReferences);
    }

    private async Task<(MovieDraft Draft, List<DraftImage> Images)> SeedDraftAsync(DraftStatus status, params string[] blobReferences)
    {
        Seller seller = new() { Id = Guid.CreateVersion7(), SubjectId = "seller-1", CreatedAtUtc = DateTimeOffset.UtcNow };
        MovieDraft draft = new()
        {
            Id = Guid.CreateVersion7(),
            SellerId = seller.Id,
            Title = "A Movie",
            Genre = "Drama",
            Description = "A description.",
            YearOfRelease = 2026,
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        List<DraftImage> images = [];
        for (int i = 0; i < blobReferences.Length; i++)
        {
            DraftImage image = new() { Id = Guid.CreateVersion7(), DraftId = draft.Id, BlobReference = blobReferences[i], Position = i };
            draft.Images.Add(image);
            images.Add(image);
        }

        db.Sellers.Add(seller);
        db.Drafts.Add(draft);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        return (draft, images);
    }

    // Mirrors the row SubmissionResultConsumer itself would already have committed
    // before marking the Draft PendingImageCleanup - the row this consumer looks up
    // to broadcast once cleanup finishes.
    private async Task<Submission> SeedApprovedSubmissionAsync(Draft draft)
    {
        Submission submission = new()
        {
            Id = Guid.CreateVersion7(),
            SellerId = draft.SellerId,
            DraftId = draft.Id,
            TitleSnapshot = "A Movie",
            KindSnapshot = DraftKind.Movie,
            SubmittedAtUtc = DateTimeOffset.UtcNow,
            RespondedAtUtc = DateTimeOffset.UtcNow,
            Status = SubmissionStatus.Approved,
            AssignedSku = "SKU-1",
        };
        db.Submissions.Add(submission);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        return submission;
    }

    private sealed class RecordingBlobContainerClient : BlobContainerClient
    {
        public List<string> DeletedBlobNames { get; } = [];

        public HashSet<string> BlobNamesToFail { get; } = [];

        public override BlobClient GetBlobClient(string blobName)
        {
            return new RecordingBlobClient(blobName, this);
        }
    }

    private sealed class RecordingBlobClient(string name, RecordingBlobContainerClient owner) : BlobClient
    {
        public override Task<Response<bool>> DeleteIfExistsAsync(
            DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None,
            BlobRequestConditions? conditions = null,
            CancellationToken cancellationToken = default)
        {
            if (owner.BlobNamesToFail.Contains(name))
            {
                throw new RequestFailedException(503, "Simulated transient failure.");
            }

            owner.DeletedBlobNames.Add(name);
            return Task.FromResult(Response.FromValue(true, new NoOpResponse()));
        }
    }

    // Minimal Response stand-in: only Dispose is ever called by the test's own
    // Response.FromValue wrapper, none of the other members are exercised.
    [SuppressMessage("Design", "CA1063", Justification = "Trivial test double; no unmanaged resources to release.")]
    private sealed class NoOpResponse : Response
    {
        public override int Status => 200;

        public override string ReasonPhrase => "OK";

        public override Stream? ContentStream { get; set; }

        public override string ClientRequestId { get; set; } = string.Empty;

        public override void Dispose()
        {
        }

        protected override bool ContainsHeader(string name)
        {
            return false;
        }

        protected override IEnumerable<HttpHeader> EnumerateHeaders()
        {
            return [];
        }

        protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
        {
            value = null;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
        {
            values = null;
            return false;
        }
    }
}

