// <copyright file="RecordingServiceBusClient.cs" company="Henrik Jensen">
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

using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// Shared Service Bus send-recording test double for any test exercising a component
// that publishes via ServiceBusClient/ServiceBusSender. Both types expose a protected
// parameterless constructor specifically to support this kind of lightweight subclass,
// with no mocking library or Service Bus emulator needed.
internal sealed class RecordingServiceBusClient : ServiceBusClient
{
    public List<ServiceBusMessage> SentMessages { get; } = [];

    // Mutable, not init-only: a test sharing a long-lived instance of this client via
    // SellerPortalWebApplicationFactory needs to flip this after construction.
    public bool ThrowOnSend { get; set; }

    // Overridable so a test can exercise a handler's catch of a specific exception
    // type (for example ServiceBusException) instead of this default.
    public Exception ExceptionToThrowOnSend { get; set; } = new InvalidOperationException("Simulated transient Service Bus send failure.");

    public override ServiceBusSender CreateSender(string queueOrTopicName)
    {
        return new RecordingServiceBusSender(SentMessages, ThrowOnSend, ExceptionToThrowOnSend);
    }
}

internal sealed class RecordingServiceBusSender(List<ServiceBusMessage> sentMessages, bool throwOnSend, Exception exceptionToThrowOnSend) : ServiceBusSender
{
    public override Task SendMessagesAsync(IEnumerable<ServiceBusMessage> messages, CancellationToken cancellationToken = default)
    {
        if (throwOnSend)
        {
            throw exceptionToThrowOnSend;
        }

        sentMessages.AddRange(messages);
        return Task.CompletedTask;
    }

    // base.DisposeAsync() throws NullReferenceException here: it closes a real AMQP
    // link this trivial test double, constructed via the protected parameterless
    // constructor, never has.
    [SuppressMessage("Design", "CA2215", Justification = "Trivial test double; no real connection for base.DisposeAsync() to close.")]
    public override ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
