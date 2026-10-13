// <copyright file="FakeServiceBusInfrastructure.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Messaging.Tests;

// ServiceBusClient/ServiceBusProcessor each expose a protected parameterless
// constructor specifically for this kind of lightweight test subclass - no mocking
// library or Service Bus emulator needed.
internal sealed class FakeServiceBusClient : ServiceBusClient
{
    public FakeServiceBusProcessor Processor { get; } = new();

    public override ServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions options)
    {
        return Processor;
    }
}

internal sealed class FakeServiceBusProcessor : ServiceBusProcessor
{
    public int StartProcessingCallCount { get; private set; }

    public int StopProcessingCallCount { get; private set; }

    // Never actually starts/stops a real receive loop - there's no broker to receive
    // from.
    public override Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        StartProcessingCallCount++;
        return Task.CompletedTask;
    }

    public override Task StopProcessingAsync(CancellationToken cancellationToken = default)
    {
        StopProcessingCallCount++;
        return Task.CompletedTask;
    }
}
