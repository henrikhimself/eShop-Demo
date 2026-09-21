// <copyright file="SellerPendingInventoryReportStore.cs" company="Henrik Jensen">
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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Hj.EShop.Messaging;

namespace Hj.EShop.DevTools.Services;

// In-memory view of "seller-inventories": SellerInventoryReportSimulatorConsumer adds
// entries while draining the queue, the tool page lists them, and Confirm/Fail removes
// them after publishing the outcome. Mirrors SellerPendingSubmissionStore's pattern.
internal sealed class SellerPendingInventoryReportStore
{
    // Keyed by (SellerId, Sku) since InventoryReportMessage has no single id field.
    // Re-reporting the same SKU replaces the pending row, matching the Bff's own upsert
    // behavior.
    private readonly ConcurrentDictionary<(Guid SellerId, string Sku), SellerPendingInventoryReport> _reports = new();

    public void Add(InventoryReportMessage message)
    {
        _reports[(message.SellerId, message.Sku)] = new SellerPendingInventoryReport(message, DateTimeOffset.UtcNow);
    }

    public bool TryGet(Guid sellerId, string sku, [NotNullWhen(true)] out SellerPendingInventoryReport? report)
    {
        return _reports.TryGetValue((sellerId, sku), out report);
    }

    public bool Remove(Guid sellerId, string sku)
    {
        return _reports.TryRemove((sellerId, sku), out _);
    }

    public IReadOnlyCollection<SellerPendingInventoryReport> GetAll()
    {
        return [.. _reports.Values.OrderBy(r => r.ReceivedAtUtc)];
    }
}

internal sealed record SellerPendingInventoryReport(InventoryReportMessage Message, DateTimeOffset ReceivedAtUtc);
