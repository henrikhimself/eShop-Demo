// <copyright file="SellerDraftApprovalSimulatorConsumer.cs" company="Henrik Jensen">
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

// Drains "seller-submissions" into SellerPendingSubmissionStore and completes each
// message immediately. The store itself is the simulator's queue view from that point on.
internal sealed class SellerDraftApprovalSimulatorConsumer(
    ServiceBusClient client,
    SellerPendingSubmissionStore store,
    IHubContext<SellerSubmissionsHub> hubContext,
    ILogger<SellerDraftApprovalSimulatorConsumer> logger)
    : ServiceBusQueueConsumer(client, KnownNames.ResourceSellerSubmissions, logger)
{
    public override async Task<MessageHandlingResult> HandleMessageAsync(BinaryData body, CancellationToken cancellationToken)
    {
        SubmissionRequestMessage message = JsonSerializer.Deserialize<SubmissionRequestMessage>(body.ToString())
            ?? throw new InvalidOperationException("The submission request message body was empty.");

        store.Add(message);
        await hubContext.Clients.All.SendAsync(SellerSubmissionsHub.SubmissionsChangedEvent, cancellationToken);

        return MessageHandlingResult.Handled;
    }
}
