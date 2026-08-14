// <copyright file="SubmissionResultConsumerTests.cs" company="Henrik Jensen">
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

using System.Text.Json;
using System.Threading.Channels;
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
// directly. ServiceBusClient/ServiceBusSender expose a protected parameterless
// constructor specifically to support this kind of lightweight test subclass, with no
// mocking library or Service Bus emulator needed.
public sealed class SubmissionResultConsumerTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly SubmissionNotificationBroadcaster broadcaster = new();
    private readonly InventoryNotificationBroadcaster inventoryBroadcaster = new();
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
    public async Task HandleMessageAsync_Approved_MarksDraftPendingCleanupAndQueuesBlobDeletionMessages()
    {
        (Seller seller, MovieDraft draft, Submission submission) = await SeedSubmittedDraftAsync("blob-1", "blob-2");
        Channel<SubmissionSummary> channel = broadcaster.Subscribe(seller.Id);

        RecordingServiceBusClient serviceBusClient = new();
        SubmissionResultConsumer consumer = new(
            serviceBusClient, scopeFactory, broadcaster, inventoryBroadcaster, NullLogger<SubmissionResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new
        {
            SubmissionId = submission.Id,
            Approved = true,
            AssignedSku = "SKU-APPROVED",
            RejectionReason = (string?)null,
        }));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);

        // The Draft is not removed here - only marked. Removing it before its blobs are
        // confirmed deleted would leak them; the Images stay put as the durable record
        // SubmissionImageDeletionConsumer needs.
        Draft reloaded = await db.Drafts.SingleAsync(d => d.Id == draft.Id, TestContext.Current.CancellationToken);
        Assert.Equal(DraftStatus.PendingImageCleanup, reloaded.Status);
        Assert.Equal(["blob-1", "blob-2"], reloaded.Images.Select(i => i.BlobReference).OrderBy(r => r));

        Assert.Equal(
            ["blob-1", "blob-2"],
            serviceBusClient.SentMessages
                .Select(m => JsonSerializer.Deserialize<BlobDeletionMessage>(m.Body.ToString())!.BlobReference)
                .OrderBy(r => r));
        Assert.All(
            serviceBusClient.SentMessages,
            m => Assert.Equal(draft.Id, JsonSerializer.Deserialize<BlobDeletionMessage>(m.Body.ToString())!.DraftId));

        Assert.True(channel.Reader.TryRead(out SubmissionSummary? publishedSummary));
        Assert.Equal(submission.Id, publishedSummary!.Id);
        Assert.Equal(SubmissionStatus.Approved, publishedSummary.Status);
        Assert.Equal("SKU-APPROVED", publishedSummary.AssignedSku);
    }

    [Fact]
    public async Task HandleMessageAsync_ApprovedWithNoImages_RemovesDraftImmediately()
    {
        (Seller seller, MovieDraft draft, Submission submission) = await SeedSubmittedDraftAsync();

        RecordingServiceBusClient serviceBusClient = new();
        SubmissionResultConsumer consumer = new(
            serviceBusClient, scopeFactory, broadcaster, inventoryBroadcaster, NullLogger<SubmissionResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new
        {
            SubmissionId = submission.Id,
            Approved = true,
            AssignedSku = "SKU-APPROVED",
            RejectionReason = (string?)null,
        }));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Assert.Empty(await db.Drafts.Where(d => d.Id == draft.Id).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(serviceBusClient.SentMessages);
        _ = seller;
    }

    [Fact]
    public async Task HandleMessageAsync_Approved_CommitsDraftStatusBeforePublishing()
    {
        (Seller seller, MovieDraft draft, Submission submission) = await SeedSubmittedDraftAsync("blob-1");

        RecordingServiceBusClient serviceBusClient = new() { ThrowOnSend = true };
        SubmissionResultConsumer consumer = new(
            serviceBusClient, scopeFactory, broadcaster, inventoryBroadcaster, NullLogger<SubmissionResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new
        {
            SubmissionId = submission.Id,
            Approved = true,
            AssignedSku = "SKU-APPROVED",
            RejectionReason = (string?)null,
        }));

        // Propagates uncaught, same as any other transient send failure - the queue's
        // own redelivery is what retries this, not application code.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken));

        // The regression this test is for: the status change must already be durable
        // by the time the send is even attempted, so a permanent publish failure never
        // leaves a Draft that still looks fully active with blobs about to vanish.
        Draft reloaded = await db.Drafts.SingleAsync(d => d.Id == draft.Id, TestContext.Current.CancellationToken);
        Assert.Equal(DraftStatus.PendingImageCleanup, reloaded.Status);
        _ = seller;
    }

    [Fact]
    public async Task HandleMessageAsync_Rejected_RecyclesDraftAndQueuesNoBlobDeletionMessages()
    {
        (Seller seller, MovieDraft draft, Submission submission) = await SeedSubmittedDraftAsync("blob-1");
        Channel<SubmissionSummary> channel = broadcaster.Subscribe(seller.Id);

        RecordingServiceBusClient serviceBusClient = new();
        SubmissionResultConsumer consumer = new(
            serviceBusClient, scopeFactory, broadcaster, inventoryBroadcaster, NullLogger<SubmissionResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new
        {
            SubmissionId = submission.Id,
            Approved = false,
            AssignedSku = (string?)null,
            RejectionReason = "Needs a better description.",
        }));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Draft reloaded = await db.Drafts.SingleAsync(d => d.Id == draft.Id, TestContext.Current.CancellationToken);
        Assert.Equal(DraftStatus.Draft, reloaded.Status);
        Assert.Empty(serviceBusClient.SentMessages);

        Assert.True(channel.Reader.TryRead(out SubmissionSummary? publishedSummary));
        Assert.Equal(submission.Id, publishedSummary!.Id);
        Assert.Equal(SubmissionStatus.Rejected, publishedSummary.Status);
        Assert.Equal("Needs a better description.", publishedSummary.RejectionReason);
    }

    [Fact]
    public async Task HandleMessageAsync_UnknownSubmission_ReturnsUnknownRecordWithoutThrowing()
    {
        RecordingServiceBusClient serviceBusClient = new();
        SubmissionResultConsumer consumer = new(
            serviceBusClient, scopeFactory, broadcaster, inventoryBroadcaster, NullLogger<SubmissionResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new
        {
            SubmissionId = Guid.CreateVersion7(),
            Approved = true,
            AssignedSku = (string?)null,
            RejectionReason = (string?)null,
        }));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.UnknownRecord, result);
    }

    [Fact]
    public async Task HandleMessageAsync_SubmissionAlreadyCancelled_IsANoOpAndDoesNotTouchTheResumedDraft()
    {
        // The Seller can cancel a review (SubmissionEndpoints' cancel-review
        // endpoint), returning the Draft to Draft status so they can resume editing.
        // A late Approve/Reject result for that same, now-cancelled submission must
        // not be applied - doing so could delete/mark-for-cleanup a Draft the Seller
        // has since resumed editing.
        (Seller seller, MovieDraft draft, Submission submission) = await SeedSubmittedDraftAsync("blob-1");
        Submission trackedSubmission = await db.Submissions.SingleAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Draft trackedDraft = await db.Drafts.SingleAsync(d => d.Id == draft.Id, TestContext.Current.CancellationToken);
        trackedSubmission.Status = SubmissionStatus.Cancelled;
        trackedDraft.Status = DraftStatus.Draft;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        Channel<SubmissionSummary> channel = broadcaster.Subscribe(seller.Id);

        RecordingServiceBusClient serviceBusClient = new();
        SubmissionResultConsumer consumer = new(
            serviceBusClient, scopeFactory, broadcaster, inventoryBroadcaster, NullLogger<SubmissionResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new
        {
            SubmissionId = submission.Id,
            Approved = true,
            AssignedSku = "SKU-LATE",
            RejectionReason = (string?)null,
        }));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Submission reloadedSubmission = await db.Submissions.SingleAsync(s => s.Id == submission.Id, TestContext.Current.CancellationToken);
        Assert.Equal(SubmissionStatus.Cancelled, reloadedSubmission.Status);
        Assert.Null(reloadedSubmission.AssignedSku);
        Draft reloadedDraft = await db.Drafts.SingleAsync(d => d.Id == draft.Id, TestContext.Current.CancellationToken);
        Assert.Equal(DraftStatus.Draft, reloadedDraft.Status);
        Assert.Empty(serviceBusClient.SentMessages);
        Assert.False(channel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task HandleMessageAsync_Approved_BroadcastsInventorySummaryForTheAssignedSku()
    {
        (Seller seller, MovieDraft draft, Submission submission) = await SeedSubmittedDraftAsync();
        Channel<InventorySummary> inventoryChannel = inventoryBroadcaster.Subscribe(seller.Id);

        RecordingServiceBusClient serviceBusClient = new();
        SubmissionResultConsumer consumer = new(
            serviceBusClient, scopeFactory, broadcaster, inventoryBroadcaster, NullLogger<SubmissionResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new
        {
            SubmissionId = submission.Id,
            Approved = true,
            AssignedSku = "SKU-NEW",
            RejectionReason = (string?)null,
        }));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Assert.True(inventoryChannel.Reader.TryRead(out InventorySummary? summary));
        Assert.Equal("SKU-NEW", summary!.Sku);
        Assert.Equal(draft.Title, summary.TitleSnapshot);
        Assert.Null(summary.ReportedQuantity);
        Assert.Null(summary.SyncStatus);
    }

    [Fact]
    public async Task HandleMessageAsync_ApprovedForAnAlreadyReportedSku_BroadcastsTheExistingInventoryData()
    {
        // The deduplication process (SPEC.md) can assign an existing SKU, not always a
        // brand new one - the broadcast must reflect that SKU's existing report, not
        // an empty one.
        (Seller seller, MovieDraft draft, Submission submission) = await SeedSubmittedDraftAsync();
        db.SellerInventories.Add(new SellerInventory
        {
            Id = Guid.CreateVersion7(),
            SellerId = seller.Id,
            Sku = "SKU-EXISTING",
            ReportedQuantity = 42,
            SyncStatus = InventorySyncStatus.Confirmed,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        Channel<InventorySummary> inventoryChannel = inventoryBroadcaster.Subscribe(seller.Id);

        RecordingServiceBusClient serviceBusClient = new();
        SubmissionResultConsumer consumer = new(
            serviceBusClient, scopeFactory, broadcaster, inventoryBroadcaster, NullLogger<SubmissionResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new
        {
            SubmissionId = submission.Id,
            Approved = true,
            AssignedSku = "SKU-EXISTING",
            RejectionReason = (string?)null,
        }));

        await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.True(inventoryChannel.Reader.TryRead(out InventorySummary? summary));
        Assert.Equal("SKU-EXISTING", summary!.Sku);
        Assert.Equal(42, summary.ReportedQuantity);
        Assert.Equal(InventorySyncStatus.Confirmed, summary.SyncStatus);
        _ = draft;
    }

    [Fact]
    public async Task HandleMessageAsync_Rejected_DoesNotBroadcastToInventory()
    {
        (Seller seller, _, Submission submission) = await SeedSubmittedDraftAsync();
        Channel<InventorySummary> inventoryChannel = inventoryBroadcaster.Subscribe(seller.Id);

        RecordingServiceBusClient serviceBusClient = new();
        SubmissionResultConsumer consumer = new(
            serviceBusClient, scopeFactory, broadcaster, inventoryBroadcaster, NullLogger<SubmissionResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new
        {
            SubmissionId = submission.Id,
            Approved = false,
            AssignedSku = (string?)null,
            RejectionReason = "Needs a better description.",
        }));

        await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.False(inventoryChannel.Reader.TryRead(out _));
    }

    private async Task<(Seller Seller, MovieDraft Draft, Submission Submission)> SeedSubmittedDraftAsync(params string[] blobReferences)
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
            Status = DraftStatus.PendingReview,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        for (int i = 0; i < blobReferences.Length; i++)
        {
            draft.Images.Add(new DraftImage { Id = Guid.CreateVersion7(), DraftId = draft.Id, BlobReference = blobReferences[i], Position = i });
        }

        Submission submission = new()
        {
            Id = Guid.CreateVersion7(),
            SellerId = seller.Id,
            DraftId = draft.Id,
            TitleSnapshot = draft.Title,
            KindSnapshot = DraftKind.Movie,
            SubmittedAtUtc = DateTimeOffset.UtcNow,
            Status = SubmissionStatus.Pending,
        };

        db.Sellers.Add(seller);
        db.Drafts.Add(draft);
        db.Submissions.Add(submission);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        return (seller, draft, submission);
    }
}
