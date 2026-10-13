// <copyright file="SubmissionResultConsumer.cs" company="Henrik Jensen">
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
using Azure.Messaging.ServiceBus;
using Hj.EShop.Common;
using Hj.EShop.Messaging;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Endpoints;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Hj.EShop.SellerPortal.Bff.Messaging;

internal partial class SubmissionResultConsumer(
    ServiceBusClient client,
    IServiceScopeFactory scopeFactory,
    SubmissionNotificationBroadcaster broadcaster,
    InventoryNotificationBroadcaster inventoryBroadcaster,
    ILogger<SubmissionResultConsumer> logger)
    : ServiceBusQueueConsumer(client, KnownNames.ResourceSellerSubmissionsResult, logger)
{
    // A distinct field, not the primary constructor's own "client" parameter used
    // directly: the compiler flags reusing a primary-constructor parameter both as a
    // base-constructor argument and inside this class's own members (CS9107), since the
    // base class might also capture it separately.
    private readonly ServiceBusClient _serviceBusClient = client;

    public override async Task<MessageHandlingResult> HandleMessageAsync(BinaryData body, CancellationToken cancellationToken)
    {
        SubmissionResultMessage message = JsonSerializer.Deserialize<SubmissionResultMessage>(body.ToString())
            ?? throw new InvalidOperationException("The submission result message body was empty.");

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();

        Submission? submission = await db.Submissions
            .FirstOrDefaultAsync(s => s.Id == message.SubmissionId, cancellationToken);

        if (submission is null)
        {
            LogReceivedResultForUnknownSubmission(logger, message.SubmissionId);
            return MessageHandlingResult.UnknownRecord;
        }

        if (submission.Status != SubmissionStatus.Pending)
        {
            // Expected race, not a corrupt message: the Seller can cancel a review
            // (SubmissionEndpoints.CancelReview) while an Approve/Reject result for
            // that same submission is already in flight. Applying it now would act on
            // a Draft the Seller has since resumed editing.
            LogIgnoringSubmissionResultBecauseNotPending(logger, message.SubmissionId, submission.Status);
            return MessageHandlingResult.Handled;
        }

        submission.Status = message.Approved ? SubmissionStatus.Approved : SubmissionStatus.Rejected;
        submission.AssignedSku = message.AssignedSku;
        submission.RejectionReason = message.RejectionReason;
        submission.RespondedAtUtc = DateTimeOffset.UtcNow;

        Draft? draft = submission.DraftId is Guid draftId
            ? await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId, cancellationToken)
            : null;
        bool hasImagesToClean = draft is not null && submission.Status == SubmissionStatus.Approved && draft.Images.Count > 0;

        if (draft is not null)
        {
            if (submission.Status == SubmissionStatus.Approved)
            {
                if (hasImagesToClean)
                {
                    // Marked, not removed: the Images are the durable record
                    // SubmissionImageDeletionConsumer needs to find and delete the
                    // right blobs. Removing the Draft here, before its blobs are
                    // confirmed gone, would leak them.
                    draft.Status = DraftStatus.PendingImageCleanup;
                }
                else
                {
                    // No images to wait for - safe to remove immediately.
                    db.Drafts.Remove(draft);
                }
            }
            else
            {
                draft.Status = DraftStatus.Draft;
                draft.LastRejectionReason = message.RejectionReason;
            }
        }

        // Commits before any blob-deletion message is published, not after: if this
        // throws, the message isn't completed, Service Bus redelivers, and nothing has
        // changed yet - a clean full retry. Publishing first would let a blob actually
        // get deleted before this state was durable.
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Publish(submission.SellerId, SubmissionEndpoints.ToSummary(submission));

        // An Approved SKU becomes reportable in Inventory immediately, so broadcast a
        // summary for it too. Looked up rather than assumed empty - the SKU can be one
        // the Seller already reported against.
        if (submission.Status == SubmissionStatus.Approved && submission.AssignedSku is { } assignedSku)
        {
            SellerInventory? inventory = await db.SellerInventories.FirstOrDefaultAsync(
                i => i.SellerId == submission.SellerId && i.Sku == assignedSku, cancellationToken);
            inventoryBroadcaster.Publish(submission.SellerId, InventoryEndpoints.ToSummary(assignedSku, submission.TitleSnapshot, inventory));
        }

        if (draft is not null && hasImagesToClean)
        {
            await DraftImageCleanup.PublishDeletionMessagesAsync(_serviceBusClient, draft.Id, draft.Images, cancellationToken);
        }

        return MessageHandlingResult.Handled;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Received a submission result for unknown submission {SubmissionId}.")]
    private static partial void LogReceivedResultForUnknownSubmission(ILogger logger, Guid submissionId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Ignoring a submission result for {SubmissionId}: its status is already {Status}, not Pending.")]
    private static partial void LogIgnoringSubmissionResultBecauseNotPending(ILogger logger, Guid submissionId, SubmissionStatus status);
}
