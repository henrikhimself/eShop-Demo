// <copyright file="Index.cshtml.cs" company="Henrik Jensen">
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
using Hj.EShop.DevTools.Services;
using Hj.EShop.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace Hj.EShop.DevTools.Pages.Tools.SellerInventoryReportSimulator;

internal sealed class IndexModel(
    SellerPendingInventoryReportStore store, ServiceBusClient serviceBusClient, IHubContext<SellerInventoryHub> hubContext) : PageModel
{
    public IReadOnlyCollection<SellerPendingInventoryReport> PendingReports { get; private set; } = [];

    public void OnGet()
    {
        PendingReports = store.GetAll();
    }

    public async Task<IActionResult> OnPostConfirmAsync(Guid sellerId, string sku, CancellationToken cancellationToken)
    {
        if (!store.TryGet(sellerId, sku, out _))
        {
            return RedirectToPage();
        }

        InventoryResultMessage message = new(sellerId, sku, Confirmed: true, Error: null);
        await PublishAsync(message, cancellationToken);
        store.Remove(sellerId, sku);
        await hubContext.Clients.All.SendAsync(SellerInventoryHub.InventoryChangedEvent, cancellationToken);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostFailAsync(Guid sellerId, string sku, string error, CancellationToken cancellationToken)
    {
        if (!store.TryGet(sellerId, sku, out _))
        {
            return RedirectToPage();
        }

        InventoryResultMessage message = new(sellerId, sku, Confirmed: false, error);
        await PublishAsync(message, cancellationToken);
        store.Remove(sellerId, sku);
        await hubContext.Clients.All.SendAsync(SellerInventoryHub.InventoryChangedEvent, cancellationToken);

        return RedirectToPage();
    }

    private async Task PublishAsync(InventoryResultMessage message, CancellationToken cancellationToken)
    {
        await using ServiceBusSender sender = serviceBusClient.CreateSender(KnownNames.ResourceSellerInventoriesResult);
        await sender.SendMessagesAsync([new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(message))], cancellationToken);
    }
}
