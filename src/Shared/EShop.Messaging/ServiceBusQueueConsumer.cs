// <copyright file="ServiceBusQueueConsumer.cs" company="Henrik Jensen">
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Hj.EShop.Messaging;

public abstract class ServiceBusQueueConsumer(
    ServiceBusClient client,
    string queueName,
    ILogger logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // AutoCompleteMessages defaults to true, which would race the explicit CompleteMessageAsync/DeadLetterMessageAsync calls below.
        _processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions { AutoCompleteMessages = false });
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        // See doc/MEMORY.md — retries a not-yet-reachable Service Bus namespace instead of crashing the host.
        ResiliencePipeline startupRetryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                MaxRetryAttempts = int.MaxValue,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Retrying StartProcessingAsync for queue {QueueName} (attempt {AttemptNumber}).",
                        queueName,
                        args.AttemptNumber + 1);
                    return default;
                },
            })
            .Build();

        await startupRetryPipeline.ExecuteAsync(
            ct => new ValueTask(_processor.StartProcessingAsync(ct)),
            stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    // Public, not protected: lets a test project call a consumer's handling logic directly, without constructing a real ServiceBusProcessor's ProcessMessageEventArgs.
    public abstract Task<MessageHandlingResult> HandleMessageAsync(BinaryData body, CancellationToken cancellationToken);

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        MessageHandlingResult result = await HandleMessageAsync(args.Message.Body, args.CancellationToken);

        if (result == MessageHandlingResult.UnknownRecord)
        {
            // The target record doesn't exist and never will, so dead-letter for manual reconciliation instead of retrying via redelivery.
            logger.LogWarning("Dead-lettering an unresolvable message from queue {QueueName}.", queueName);
            await args.DeadLetterMessageAsync(args.Message, deadLetterReason: "UnknownRecord", cancellationToken: args.CancellationToken);
            return;
        }

        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Error processing a message from queue {QueueName}.", queueName);
        return Task.CompletedTask;
    }
}

public enum MessageHandlingResult
{
    Handled,
    UnknownRecord,
}
