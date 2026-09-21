// <copyright file="InventoryResultConsumer.cs" company="Henrik Jensen">
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

internal partial class InventoryResultConsumer(
    ServiceBusClient client,
    IServiceScopeFactory scopeFactory,
    InventoryNotificationBroadcaster broadcaster,
    ILogger<InventoryResultConsumer> logger)
    : ServiceBusQueueConsumer(client, KnownNames.ResourceSellerInventoriesResult, logger)
{
    public override async Task<MessageHandlingResult> HandleMessageAsync(BinaryData body, CancellationToken cancellationToken)
    {
        InventoryResultMessage message = JsonSerializer.Deserialize<InventoryResultMessage>(body.ToString())
            ?? throw new InvalidOperationException("The inventory result message body was empty.");

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();

        SellerInventory? inventory = await db.SellerInventories.FirstOrDefaultAsync(
            i => i.SellerId == message.SellerId && i.Sku == message.Sku,
            cancellationToken);

        if (inventory is null)
        {
            LogReceivedInventoryResultForUnknownSellerSku(logger, message.SellerId, message.Sku);
            return MessageHandlingResult.UnknownRecord;
        }

        inventory.SyncStatus = message.Confirmed ? InventorySyncStatus.Confirmed : InventorySyncStatus.Failed;
        inventory.LastSyncedAtUtc = DateTimeOffset.UtcNow;
        inventory.LastError = message.Error;

        await db.SaveChangesAsync(cancellationToken);

        // Same title-snapshot lookup InventoryEndpoints' GET route uses: the most
        // recently responded-to Approved submission for this Seller/SKU. Ordered
        // client-side, not via OrderBy in the query - SQLite (used in tests) cannot
        // order by DateTimeOffset in SQL.
        List<Submission> approvedSubmissions = await db.Submissions
            .Where(s => s.SellerId == message.SellerId && s.Status == SubmissionStatus.Approved && s.AssignedSku == message.Sku)
            .ToListAsync(cancellationToken);
        string? titleSnapshot = approvedSubmissions.OrderByDescending(s => s.RespondedAtUtc).FirstOrDefault()?.TitleSnapshot;

        if (titleSnapshot is not null)
        {
            // Lets any open connection for this Seller (e.g. the Inventory page)
            // learn about the Confirmed/Failed result live.
            broadcaster.Publish(message.SellerId, InventoryEndpoints.ToSummary(message.Sku, titleSnapshot, inventory));
        }

        return MessageHandlingResult.Handled;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Received an inventory result for unknown seller/SKU {SellerId}/{Sku}.")]
    private static partial void LogReceivedInventoryResultForUnknownSellerSku(ILogger logger, Guid sellerId, string sku);
}
