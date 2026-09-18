// <copyright file="ServiceBusQueueConsumerTests.cs" company="Henrik Jensen">
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

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hj.EShop.Messaging.Tests;

// Exercises ServiceBusQueueConsumer's own generic start/stop wiring via a minimal
// concrete subclass - not any concrete app-specific consumer's business logic, which is
// covered where that logic actually lives (EShop.SellerPortal.Bff.Tests,
// EShop.DevTools.Tests). Deliberately not covered here: the dead-letter-vs-complete
// decision (ProcessMessageAsync) and the error-handling path (ProcessErrorAsync/
// OnProcessErrorAsync) - both need a real Azure.Messaging.ServiceBus
// ProcessMessageEventArgs/ProcessErrorEventArgs built against a ServiceBusProcessor
// constructed via its "for mocking" constructor, and that SDK version's own internals
// intermittently threw an unexplained NullReferenceException from inside those calls.
// Rather than work around an SDK bug we don't control (or risk a flaky test giving
// false assurance), that coverage was dropped; HandleMessageAsync's return value is
// what actually drives the dead-letter-vs-complete decision anyway, and is covered by
// each concrete consumer's own tests.
public sealed class ServiceBusQueueConsumerTests
{
    [Fact]
    public async Task StartAsync_StartsProcessingOnTheProcessorItCreated()
    {
        FakeServiceBusClient client = new();
        TestConsumer consumer = new(client, (_, _) => Task.FromResult(MessageHandlingResult.Handled));

        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => client.Processor.StartProcessingCallCount > 0);

        Assert.Equal(1, client.Processor.StartProcessingCallCount);
    }

    [Fact]
    public async Task StopAsync_StopsProcessing()
    {
        FakeServiceBusClient client = new();
        TestConsumer consumer = new(client, (_, _) => Task.FromResult(MessageHandlingResult.Handled));
        await consumer.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => client.Processor.StartProcessingCallCount > 0);

        await consumer.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, client.Processor.StopProcessingCallCount);
    }

    // BackgroundService.StartAsync only guarantees ExecuteAsync has been invoked, not
    // that it has reached any particular await point - the Polly-wrapped
    // StartProcessingAsync call happens on a later continuation, not synchronously
    // before StartAsync returns. Polls instead of asserting immediately to avoid a
    // flaky race against that continuation.
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeoutSource = new(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeoutSource.Token);
        }
    }

    private sealed class TestConsumer(ServiceBusClient client, Func<BinaryData, CancellationToken, Task<MessageHandlingResult>> handleMessage)
        : ServiceBusQueueConsumer(client, "a-queue", NullLogger.Instance)
    {
        public override Task<MessageHandlingResult> HandleMessageAsync(BinaryData body, CancellationToken cancellationToken)
        {
            return handleMessage(body, cancellationToken);
        }
    }
}
