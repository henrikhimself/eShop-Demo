// <copyright file="SubmissionImageDeletionConsumer.cs" company="Henrik Jensen">
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
using Azure.Storage.Blobs;
using Hj.EShop.Common;
using Hj.EShop.Messaging;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Endpoints;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Hj.EShop.SellerPortal.Bff.Messaging;

// Deletes one submission-image blob per message (ADR 0009's claim-check), published by
// SubmissionResultConsumer once it has durably marked the owning Draft
// PendingImageCleanup. A delete failure is left uncaught so the queue's own
// MaxDeliveryCount (see AppHost.cs) governs retry/dead-letter, not application code.
internal sealed class SubmissionImageDeletionConsumer(
    ServiceBusClient client,
    IServiceScopeFactory scopeFactory,
    BlobContainerClient submissionsImageContainer,
    SubmissionNotificationBroadcaster broadcaster,
    ILogger<SubmissionImageDeletionConsumer> logger)
    : ServiceBusQueueConsumer(client, KnownNames.ResourceSellerSubmissionsImageDeletions, logger)
{
    public override async Task<MessageHandlingResult> HandleMessageAsync(BinaryData body, CancellationToken cancellationToken)
    {
        BlobDeletionMessage message = JsonSerializer.Deserialize<BlobDeletionMessage>(body.ToString())
            ?? throw new InvalidOperationException("The blob deletion message body was empty.");

        // Blob delete before the DB update below, not after: a delete failure here
        // still leaves the DraftImage row in place for a clean redelivery retry (same
        // reasoning as SubmissionResultConsumer's own commit-before-publish ordering).
        await submissionsImageContainer.GetBlobClient(message.BlobReference).DeleteIfExistsAsync(cancellationToken: cancellationToken);

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();

        Draft? draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == message.DraftId, cancellationToken);
        if (draft is null)
        {
            // Already fully cleaned up by an earlier delivery of this same message.
            return MessageHandlingResult.Handled;
        }

        DraftImage? image = draft.Images.FirstOrDefault(i => i.Id == message.DraftImageId);
        if (image is not null)
        {
            draft.Images.Remove(image);
        }

        bool isFullyCleanedUp = draft.Images.Count == 0 && draft.Status == DraftStatus.PendingImageCleanup;
        Submission? submission = null;

        if (isFullyCleanedUp)
        {
            // Only SubmissionResultConsumer's approval cleanup sets PendingImageCleanup,
            // so a manual single-image delete never triggers this. Looked up before the
            // Draft is removed - the only handle left for broadcasting below.
            submission = await db.Submissions.FirstOrDefaultAsync(
                s => s.DraftId == draft.Id && s.Status == SubmissionStatus.Approved, cancellationToken);

            db.Drafts.Remove(draft);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (submission is not null)
        {
            // Lets an open edit page for this now-removed Draft learn about it live:
            // it stays subscribed through PendingImageCleanup for exactly this
            // message (see the movie/merchandise edit pages' SSE effect), then
            // refetches, gets a 404, and shows its "Approved" panel.
            broadcaster.Publish(draft.SellerId, SubmissionEndpoints.ToSummary(submission));
        }

        return MessageHandlingResult.Handled;
    }
}

