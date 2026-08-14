// <copyright file="InventoryEndpoints.cs" company="Henrik Jensen">
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
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Hj.EShop.SellerPortal.Bff.Endpoints;

// The Seller's inventory-reporting feature (ADR 0007) - separate from the draft/
// submission workflow, and only reachable for a SKU the Merchandiser has already
// approved for this Seller.
internal static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/bff/api/inventory", async (HttpContext context, SellerPortalDbContext db, CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            // Loaded into memory (not ordered/grouped in SQL): SQLite (used in tests)
            // cannot order by DateTimeOffset in SQL - same reasoning as
            // SubmissionEndpoints' /bff/api/submissions route. Fine at this app's
            // per-seller approved-SKU volume.
            List<Submission> approvedSubmissions = await db.Submissions
                .Where(s => s.SellerId == seller.Id && s.Status == SubmissionStatus.Approved && s.AssignedSku != null)
                .ToListAsync(cancellationToken);

            List<SellerInventory> inventories = await db.SellerInventories
                .Where(i => i.SellerId == seller.Id)
                .ToListAsync(cancellationToken);
            var inventoryBySku = inventories.ToDictionary(i => i.Sku);

            List<InventorySummary> summaries = [.. approvedSubmissions
                .GroupBy(s => s.AssignedSku!)
                .Select(group =>
                {
                    // A SKU can have more than one Approved submission behind it over
                    // time (e.g. a Seller re-submits after a Draft edit) - the most
                    // recently responded-to one is the best title to show.
                    Submission latest = group.OrderByDescending(s => s.RespondedAtUtc).First();
                    inventoryBySku.TryGetValue(group.Key, out SellerInventory? inventory);
                    return ToSummary(group.Key, latest.TitleSnapshot, inventory);
                })
                .OrderBy(s => s.Sku, StringComparer.Ordinal)];

            return Results.Ok(summaries);
        })
        .RequireAuthorization("SellerOnly")
        .Produces<IReadOnlyList<InventorySummary>>(StatusCodes.Status200OK);

        endpoints.MapPost("/bff/api/inventory/{sku}", async (
            string sku,
            ReportInventoryRequest request,
            HttpContext context,
            SellerPortalDbContext db,
            ServiceBusClient serviceBusClient,
            InventoryNotificationBroadcaster broadcaster,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            if (request.Quantity < 0)
            {
                return Results.BadRequest("Quantity cannot be negative.");
            }

            // Not found (rather than 403) for both "no such SKU" and "not this
            // Seller's SKU": same not-owned-means-not-found convention
            // DraftEndpoints/MerchandiseDraftEndpoints already use, so this route
            // never confirms whether a SKU exists for a different Seller.
            List<Submission> approvedSubmissions = await db.Submissions
                .Where(s => s.SellerId == seller.Id && s.Status == SubmissionStatus.Approved && s.AssignedSku == sku)
                .ToListAsync(cancellationToken);
            if (approvedSubmissions.Count == 0)
            {
                return Results.NotFound();
            }

            string titleSnapshot = approvedSubmissions.OrderByDescending(s => s.RespondedAtUtc).First().TitleSnapshot;

            SellerInventory? inventory = await db.SellerInventories.FirstOrDefaultAsync(
                i => i.SellerId == seller.Id && i.Sku == sku, cancellationToken);

            if (inventory is null)
            {
                inventory = new SellerInventory
                {
                    Id = Guid.CreateVersion7(),
                    SellerId = seller.Id,
                    Sku = sku,
                    ReportedQuantity = request.Quantity,
                    SyncStatus = InventorySyncStatus.Pending,
                };
                db.SellerInventories.Add(inventory);
            }
            else
            {
                inventory.ReportedQuantity = request.Quantity;
                inventory.SyncStatus = InventorySyncStatus.Pending;
                inventory.LastError = null;
            }

            // Commits before publish, not after - same crash-safety ordering
            // SubmissionEndpoints already uses.
            await db.SaveChangesAsync(cancellationToken);

            InventoryReportMessage message = new(seller.Id, sku, request.Quantity);
            if (await PublishInventoryReportAsync(message, serviceBusClient, logger, seller.Id, sku, cancellationToken) is { } errorResult)
            {
                return errorResult;
            }

            InventorySummary summary = ToSummary(sku, titleSnapshot, inventory);

            // Lets any other open connection for this Seller (e.g. a second tab)
            // learn about the new report live - same rationale as
            // SubmissionEndpoints' broadcast calls.
            broadcaster.Publish(seller.Id, summary);

            return Results.Ok(summary);
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces<InventorySummary>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    // Publishes the inventory report and turns a broker failure into a 500, keeping
    // the already-committed SellerInventory row as-is (see the crash-safety comment
    // above the call site). Returns null on success.
    private static async Task<IResult?> PublishInventoryReportAsync(
        InventoryReportMessage message,
        ServiceBusClient serviceBusClient,
        ILogger<Program> logger,
        Guid sellerId,
        string sku,
        CancellationToken cancellationToken)
    {
        try
        {
            await using ServiceBusSender sender = serviceBusClient.CreateSender(KnownNames.ResourceSellerInventories);
            await sender.SendMessagesAsync(
                [new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(message))], cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Matches SubmissionEndpoints.PublishSubmissionRequestAsync: any transport
            // exception hits this log-and-500 path. The SellerInventory row above is
            // already durable - no compensating transaction exists yet.
            logger.LogError(
                exception, "Failed to publish an inventory report for seller {SellerId}, SKU {Sku}.", sellerId, sku);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        return null;
    }

    internal static InventorySummary ToSummary(string sku, string titleSnapshot, SellerInventory? inventory)
    {
        return new InventorySummary(
            sku,
            titleSnapshot,
            inventory?.ReportedQuantity,
            inventory?.SyncStatus,
            inventory?.LastSyncedAtUtc,
            inventory?.LastError);
    }
}
