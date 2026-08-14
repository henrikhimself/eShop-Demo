// <copyright file="SellerInventoryReportSimulatorConsumer.cs" company="Henrik Jensen">
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

// Drains "seller-inventories" into SellerPendingInventoryReportStore and completes each
// message immediately. The store itself is the simulator's queue view from that point
// on. Mirrors SellerDraftApprovalSimulatorConsumer's pattern.
internal sealed class SellerInventoryReportSimulatorConsumer(
    ServiceBusClient client,
    SellerPendingInventoryReportStore store,
    IHubContext<SellerInventoryHub> hubContext,
    ILogger<SellerInventoryReportSimulatorConsumer> logger)
    : ServiceBusQueueConsumer(client, KnownNames.ResourceSellerInventories, logger)
{
    public override async Task<MessageHandlingResult> HandleMessageAsync(BinaryData body, CancellationToken cancellationToken)
    {
        InventoryReportMessage message = JsonSerializer.Deserialize<InventoryReportMessage>(body.ToString())
            ?? throw new InvalidOperationException("The inventory report message body was empty.");

        store.Add(message);
        await hubContext.Clients.All.SendAsync(SellerInventoryHub.InventoryChangedEvent, cancellationToken);

        return MessageHandlingResult.Handled;
    }
}
