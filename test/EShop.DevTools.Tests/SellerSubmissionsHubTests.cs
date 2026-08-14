// <copyright file="SellerSubmissionsHubTests.cs" company="Henrik Jensen">
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

// Connects a real HubConnection to the real Program.cs's mapped hub (via
// DevToolsWebApplicationFactory's TestServer) to prove the "submissionsChanged"
// broadcast actually reaches a connected client - not just that IHubContext.SendAsync
// was called (SellerDraftApprovalSimulatorConsumerTests already covers that call in
// isolation).
public sealed class SellerSubmissionsHubTests
{
    [Fact]
    public async Task ConsumerHandlingAMessage_BroadcastsSubmissionsChanged()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using DevToolsWebApplicationFactory factory = new();
        HubConnection connection = CreateConnection(factory);
        TaskCompletionSource broadcastReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On(SellerSubmissionsHub.SubmissionsChangedEvent, broadcastReceived.TrySetResult);

        try
        {
            await connection.StartAsync(cancellationToken);

            IHubContext<SellerSubmissionsHub> hubContext = factory.Services.GetRequiredService<IHubContext<SellerSubmissionsHub>>();
            SellerDraftApprovalSimulatorConsumer consumer = new(
                client: null!, factory.Store, hubContext, NullLogger<SellerDraftApprovalSimulatorConsumer>.Instance);
            SubmissionRequestMessage message = SellerDraftApprovalSimulatorPageTests.CreateMessage();
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
    public async Task PostApprove_BroadcastsSubmissionsChanged()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using DevToolsWebApplicationFactory factory = new();
        SubmissionRequestMessage message = SellerDraftApprovalSimulatorPageTests.CreateMessage();
        factory.Store.Add(message);
        HubConnection connection = CreateConnection(factory);
        TaskCompletionSource broadcastReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On(SellerSubmissionsHub.SubmissionsChangedEvent, broadcastReceived.TrySetResult);

        try
        {
            await connection.StartAsync(cancellationToken);

            HttpClient client = SellerDraftApprovalSimulatorPageTests.CreateClient(factory);
            string token = await SellerDraftApprovalSimulatorPageTests.GetAntiforgeryTokenAsync(client, cancellationToken);
            FormUrlEncodedContent form = new(new Dictionary<string, string>
            {
                ["submissionId"] = message.SubmissionId.ToString(),
                ["__RequestVerificationToken"] = token,
            });

            await client.PostAsync(
                new Uri("/Tools/SellerDraftApprovalSimulator?handler=Approve", UriKind.Relative), form, cancellationToken);

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
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/seller-submissions"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }
}
