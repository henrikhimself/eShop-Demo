// <copyright file="SellerInventoryReportSimulatorConsumerTests.cs" company="Henrik Jensen">
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
using Hj.EShop.DevTools.Hubs;
using Hj.EShop.DevTools.Services;
using Hj.EShop.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hj.EShop.DevTools.Tests;

// Mirrors SellerDraftApprovalSimulatorConsumerTests - see that type for the reasoning
// behind calling HandleMessageAsync directly with a bare ServiceCollection-built
// IHubContext.
public sealed class SellerInventoryReportSimulatorConsumerTests
{
    [Fact]
    public async Task HandleMessageAsync_AddsReportToStoreAndReturnsHandled()
    {
        SellerPendingInventoryReportStore store = new();
        IHubContext<SellerInventoryHub> hubContext = new ServiceCollection()
            .AddLogging()
            .AddSignalR().Services.BuildServiceProvider()
            .GetRequiredService<IHubContext<SellerInventoryHub>>();
        SellerInventoryReportSimulatorConsumer consumer = new(
            client: null!, store, hubContext, NullLogger<SellerInventoryReportSimulatorConsumer>.Instance);
        InventoryReportMessage message = new(Guid.CreateVersion7(), "SKU-1", 10);
        BinaryData body = new(JsonSerializer.SerializeToUtf8Bytes(message));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Assert.True(store.TryGet(message.SellerId, message.Sku, out SellerPendingInventoryReport? report));
        Assert.Equal(message.Quantity, report!.Message.Quantity);
    }
}
