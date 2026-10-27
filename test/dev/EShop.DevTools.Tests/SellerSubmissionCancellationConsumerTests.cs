// <copyright file="SellerSubmissionCancellationConsumerTests.cs" company="Henrik Jensen">
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

// Same "no host, no broker" shape as SellerDraftApprovalSimulatorConsumerTests - see
// that file's comment for why a bare ServiceCollection suffices for IHubContext.
public sealed class SellerSubmissionCancellationConsumerTests
{
    [Fact]
    public async Task HandleMessageAsync_RemovesPendingSubmissionAndReturnsHandled()
    {
        SellerPendingSubmissionStore store = new();
        SubmissionRequestMessage requestMessage = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), SubmissionKind.Movie, "A Movie", "A description.", "Drama", 2026,
            [new SubmissionFormatVariantPayload("Dvd", 9.99m)], AssociatedMovieTitle: null, ImageBlobReferences: ["blob-reference"],
            Price: null);
        store.Add(requestMessage);
        IHubContext<SellerSubmissionsHub> hubContext = new ServiceCollection()
            .AddLogging()
            .AddSignalR().Services.BuildServiceProvider()
            .GetRequiredService<IHubContext<SellerSubmissionsHub>>();
        SellerSubmissionCancellationConsumer consumer = new(
            client: null!, store, hubContext, NullLogger<SellerSubmissionCancellationConsumer>.Instance);
        SubmissionCancelledMessage message = new(requestMessage.SubmissionId);
        BinaryData body = new(JsonSerializer.SerializeToUtf8Bytes(message));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Assert.False(store.TryGet(requestMessage.SubmissionId, out _));
    }

    [Fact]
    public async Task HandleMessageAsync_CancellationBeforeSubmission_IsRememberedAndReturnsHandled()
    {
        SellerPendingSubmissionStore store = new();
        IHubContext<SellerSubmissionsHub> hubContext = new ServiceCollection()
            .AddLogging()
            .AddSignalR().Services.BuildServiceProvider()
            .GetRequiredService<IHubContext<SellerSubmissionsHub>>();
        SellerSubmissionCancellationConsumer consumer = new(
            client: null!, store, hubContext, NullLogger<SellerSubmissionCancellationConsumer>.Instance);
        SubmissionCancelledMessage message = new(Guid.CreateVersion7());
        BinaryData body = new(JsonSerializer.SerializeToUtf8Bytes(message));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);

        SubmissionRequestMessage requestMessage = new(
            message.SubmissionId, Guid.CreateVersion7(), SubmissionKind.Movie, "A Movie", "A description.", "Drama", 2026,
            [new SubmissionFormatVariantPayload("Dvd", 9.99m)], AssociatedMovieTitle: null, ImageBlobReferences: ["blob-reference"],
            Price: null);

        Assert.False(store.Add(requestMessage));
        Assert.False(store.TryGet(message.SubmissionId, out _));
    }

    [Fact]
    public void Add_ExpiredCancellationTombstone_AddsSubmission()
    {
        MutableTimeProvider timeProvider = new(DateTimeOffset.Parse("2026-10-13T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        SellerPendingSubmissionStore store = new(timeProvider);
        var submissionId = Guid.CreateVersion7();

        Assert.Equal(SellerSubmissionCancellationResult.Remembered, store.Cancel(submissionId));
        timeProvider.Advance(TimeSpan.FromMinutes(10));

        SubmissionRequestMessage requestMessage = new(
            submissionId, Guid.CreateVersion7(), SubmissionKind.Movie, "A Movie", "A description.", "Drama", 2026,
            [new SubmissionFormatVariantPayload("Dvd", 9.99m)], AssociatedMovieTitle: null, ImageBlobReferences: ["blob-reference"],
            Price: null);

        Assert.True(store.Add(requestMessage));
        Assert.True(store.TryGet(submissionId, out _));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }

        public void Advance(TimeSpan elapsed)
        {
            _now = _now.Add(elapsed);
        }
    }
}
