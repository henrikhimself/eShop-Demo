// <copyright file="SellerSubmissionCancellationConsumer.cs" company="Henrik Jensen">
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
using Hj.EShop.DevTools.Hubs;
using Hj.EShop.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace Hj.EShop.DevTools.Services;

// Drains "seller-submissions-cancellations" and drops the matching entry from
// SellerPendingSubmissionStore, so the simulator stops showing it as pending.
internal sealed class SellerSubmissionCancellationConsumer(
    ServiceBusClient client,
    SellerPendingSubmissionStore store,
    IHubContext<SellerSubmissionsHub> hubContext,
    ILogger<SellerSubmissionCancellationConsumer> logger)
    : ServiceBusQueueConsumer(client, KnownNames.ResourceSellerSubmissionsCancellations, logger)
{
    public override async Task<MessageHandlingResult> HandleMessageAsync(BinaryData body, CancellationToken cancellationToken)
    {
        SubmissionCancelledMessage message = JsonSerializer.Deserialize<SubmissionCancelledMessage>(body.ToString())
            ?? throw new InvalidOperationException("The submission cancellation message body was empty.");

        if (!store.Remove(message.SubmissionId))
        {
            // Expected race, not corruption: the submission may already be resolved
            // (Approved/Rejected), or its cancellation arrived after a restart drained
            // the queue.
            logger.LogWarning(
                "Received a submission cancellation for unknown or already-resolved submission {SubmissionId}.",
                message.SubmissionId);
            return MessageHandlingResult.UnknownRecord;
        }

        await hubContext.Clients.All.SendAsync(SellerSubmissionsHub.SubmissionsChangedEvent, cancellationToken);

        return MessageHandlingResult.Handled;
    }
}
