// <copyright file="InventoryContracts.cs" company="Henrik Jensen">
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

using Hj.EShop.SellerPortal.Bff.Data.Entities;

namespace Hj.EShop.SellerPortal.Bff.Contracts;

// One of the Seller's own approved SKUs, paired with its current SellerInventory row
// (null fields when the Seller has never reported a quantity for this SKU yet).
internal sealed record InventorySummary(
    string Sku,
    string TitleSnapshot,
    int? ReportedQuantity,
    InventorySyncStatus? SyncStatus,
    DateTimeOffset? LastSyncedAtUtc,
    string? LastError);

internal sealed record ReportInventoryRequest(int Quantity);
