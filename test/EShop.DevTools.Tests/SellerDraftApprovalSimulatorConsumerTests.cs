// <copyright file="SellerDraftApprovalSimulatorConsumerTests.cs" company="Henrik Jensen">
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

// Calls HandleMessageAsync directly (public override, but the containing class is
// internal - see ServiceBusQueueConsumer): no live/emulated broker connection needed.
// IHubContext<SellerSubmissionsHub> needs only AddSignalR()'s DI registration, no
// host/Kestrel/TestServer - a bare ServiceCollection keeps this test's "no host, no
// broker" character (see SellerSubmissionsHubTests for the real-broadcast-delivery test).
public sealed class SellerDraftApprovalSimulatorConsumerTests
{
    [Fact]
    public async Task HandleMessageAsync_AddsSubmissionToStoreAndReturnsHandled()
    {
        SellerPendingSubmissionStore store = new();
        IHubContext<SellerSubmissionsHub> hubContext = new ServiceCollection()
            .AddLogging()
            .AddSignalR().Services.BuildServiceProvider()
            .GetRequiredService<IHubContext<SellerSubmissionsHub>>();
        SellerDraftApprovalSimulatorConsumer consumer = new(
            client: null!, store, hubContext, NullLogger<SellerDraftApprovalSimulatorConsumer>.Instance);
        SubmissionRequestMessage message = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), SubmissionKind.Movie, "A Movie", "A description.", "Drama", 2026,
            [new SubmissionFormatVariantPayload("Dvd", 9.99m)], AssociatedMovieTitle: null, ImageBlobReferences: ["blob-reference"],
            Price: null);
        BinaryData body = new(JsonSerializer.SerializeToUtf8Bytes(message));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Assert.True(store.TryGet(message.SubmissionId, out SellerPendingSubmission? submission));
        Assert.Equal(message.TitleSnapshot, submission!.Message.TitleSnapshot);
    }
}
