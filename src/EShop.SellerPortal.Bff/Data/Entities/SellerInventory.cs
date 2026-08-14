// <copyright file="SellerInventory.cs" company="Henrik Jensen">
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

namespace Hj.EShop.SellerPortal.Bff.Data.Entities;

// A Seller's reported quantity for one of their own approved SKUs, in their own
// warehouse (ADR 0007), reported over the seller-inventories queue.
internal sealed class SellerInventory
{
    public required Guid Id { get; init; }

    public required Guid SellerId { get; init; }

    public required string Sku { get; set; }

    public required int ReportedQuantity { get; set; }

    public InventorySyncStatus SyncStatus { get; set; } = InventorySyncStatus.Pending;

    public DateTimeOffset? LastSyncedAtUtc { get; set; }

    public string? LastError { get; set; }
}
