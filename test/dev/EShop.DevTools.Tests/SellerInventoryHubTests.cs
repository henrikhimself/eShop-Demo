// <copyright file="SellerInventoryHubTests.cs" company="Henrik Jensen">
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
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hj.EShop.DevTools.Tests;

// Mirrors SellerSubmissionsHubTests - see that type for the reasoning behind connecting a
// real HubConnection to the real Program.cs's mapped hub.
public sealed class SellerInventoryHubTests
{
    [Fact]
    public async Task ConsumerHandlingAMessage_BroadcastsInventoryChanged()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using DevToolsWebApplicationFactory factory = new();
        HubConnection connection = CreateConnection(factory);
        TaskCompletionSource broadcastReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On(SellerInventoryHub.InventoryChangedEvent, broadcastReceived.TrySetResult);

        try
        {
            await connection.StartAsync(cancellationToken);

            IHubContext<SellerInventoryHub> hubContext = factory.Services.GetRequiredService<IHubContext<SellerInventoryHub>>();
            SellerInventoryReportSimulatorConsumer consumer = new(
                client: null!, factory.InventoryStore, hubContext, NullLogger<SellerInventoryReportSimulatorConsumer>.Instance);
            InventoryReportMessage message = SellerInventoryReportSimulatorPageTests.CreateMessage();
            BinaryData body = new(JsonSerializer.SerializeToUtf8Bytes(message));

            await consumer.HandleMessageAsync(body, cancellationToken);

            Task firstCompleted = await Task.WhenAny(broadcastReceived.Task, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
            Assert.Same(broadcastReceived.Task, firstCompleted);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task PostConfirm_BroadcastsInventoryChanged()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using DevToolsWebApplicationFactory factory = new();
        InventoryReportMessage message = SellerInventoryReportSimulatorPageTests.CreateMessage();
        factory.InventoryStore.Add(message);
        HubConnection connection = CreateConnection(factory);
        TaskCompletionSource broadcastReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On(SellerInventoryHub.InventoryChangedEvent, broadcastReceived.TrySetResult);

        try
        {
            await connection.StartAsync(cancellationToken);

            HttpClient client = SellerInventoryReportSimulatorPageTests.CreateClient(factory);
            string token = await SellerInventoryReportSimulatorPageTests.GetAntiforgeryTokenAsync(client, cancellationToken);
            FormUrlEncodedContent form = new(new Dictionary<string, string>
            {
                ["sellerId"] = message.SellerId.ToString(),
                ["sku"] = message.Sku,
                ["__RequestVerificationToken"] = token,
            });

            await client.PostAsync(
                new Uri("/Tools/SellerInventoryReportSimulator?handler=Confirm", UriKind.Relative), form, cancellationToken);

            Task firstCompleted = await Task.WhenAny(broadcastReceived.Task, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
            Assert.Same(broadcastReceived.Task, firstCompleted);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    // TestServer has no real socket; SignalR's WebSockets transport uses
    // ClientWebSocket directly and bypasses HttpMessageHandlerFactory entirely, so it
    // can't be routed through TestServer's in-memory handler - LongPolling can.
    private static HubConnection CreateConnection(DevToolsWebApplicationFactory factory)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/seller-inventory"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }
}
