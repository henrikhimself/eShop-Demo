// <copyright file="SubmissionNotificationBroadcasterTests.cs" company="Henrik Jensen">
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

using System.Threading.Channels;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// No broker double or mocking needed - the class has no external dependencies.
public sealed class SubmissionNotificationBroadcasterTests
{
    [Fact]
    public void Publish_DeliversOnlyToThatSellersSubscriber()
    {
        SubmissionNotificationBroadcaster broadcaster = new();
        var sellerId = Guid.CreateVersion7();
        var otherSellerId = Guid.CreateVersion7();
        Channel<SubmissionSummary> channel = broadcaster.Subscribe(sellerId);
        Channel<SubmissionSummary> otherChannel = broadcaster.Subscribe(otherSellerId);
        SubmissionSummary summary = CreateSummary();

        broadcaster.Publish(sellerId, summary);

        Assert.True(channel.Reader.TryRead(out SubmissionSummary? delivered));
        Assert.Equal(summary, delivered);
        Assert.False(otherChannel.Reader.TryRead(out _));
    }

    [Fact]
    public void Publish_NoSubscriber_IsANoOp()
    {
        SubmissionNotificationBroadcaster broadcaster = new();

        // Must not throw for a Seller with zero open connections - the design point
        // this class exists to make explicit (see SubmissionNotificationBroadcaster.cs).
        Exception? exception = Record.Exception(() => broadcaster.Publish(Guid.CreateVersion7(), CreateSummary()));
        Assert.Null(exception);
    }

    [Fact]
    public void Unsubscribe_StopsFurtherDelivery()
    {
        SubmissionNotificationBroadcaster broadcaster = new();
        var sellerId = Guid.CreateVersion7();
        Channel<SubmissionSummary> channel = broadcaster.Subscribe(sellerId);

        broadcaster.Unsubscribe(sellerId, channel);
        broadcaster.Publish(sellerId, CreateSummary());

        Assert.False(channel.Reader.TryRead(out _));
    }

    [Fact]
    public void SubscriberCount_ReflectsSubscribeAndUnsubscribe()
    {
        SubmissionNotificationBroadcaster broadcaster = new();
        var sellerId = Guid.CreateVersion7();

        Assert.Equal(0, broadcaster.SubscriberCount(sellerId));

        Channel<SubmissionSummary> firstChannel = broadcaster.Subscribe(sellerId);
        Assert.Equal(1, broadcaster.SubscriberCount(sellerId));

        Channel<SubmissionSummary> secondChannel = broadcaster.Subscribe(sellerId);
        Assert.Equal(2, broadcaster.SubscriberCount(sellerId));

        broadcaster.Unsubscribe(sellerId, firstChannel);
        Assert.Equal(1, broadcaster.SubscriberCount(sellerId));

        broadcaster.Unsubscribe(sellerId, secondChannel);
        Assert.Equal(0, broadcaster.SubscriberCount(sellerId));
    }

    [Fact]
    public async Task Subscribe_ConcurrentWithUnsubscribeThatEmptiesTheList_NeverOrphansTheNewSubscriber()
    {
        SubmissionNotificationBroadcaster broadcaster = new();
        var sellerId = Guid.CreateVersion7();

        // Subscribe must atomically get-or-create the subscriber list and add to it
        // under one lock: if Unsubscribe could concurrently empty and drop that same
        // list from the dictionary in between, the new subscriber would be added to
        // an orphaned list Publish/SubscriberCount could never find again. Racing
        // many iterations gives that hazard many chances to reproduce.
        for (int i = 0; i < 2000; i++)
        {
            Channel<SubmissionSummary> transient = broadcaster.Subscribe(sellerId);
            Channel<SubmissionSummary>? persistent = null;

            await Task.WhenAll(
                Task.Run(() => broadcaster.Unsubscribe(sellerId, transient), TestContext.Current.CancellationToken),
                Task.Run(() => persistent = broadcaster.Subscribe(sellerId), TestContext.Current.CancellationToken));

            SubmissionSummary summary = CreateSummary();
            broadcaster.Publish(sellerId, summary);

            Assert.True(persistent!.Reader.TryRead(out SubmissionSummary? delivered));
            Assert.Equal(summary, delivered);
            Assert.Equal(1, broadcaster.SubscriberCount(sellerId));

            broadcaster.Unsubscribe(sellerId, persistent);
        }

        Assert.Equal(0, broadcaster.SubscriberCount(sellerId));
    }

    private static SubmissionSummary CreateSummary()
    {
        return new SubmissionSummary(
            Guid.CreateVersion7(), "A Movie", SubmissionStatus.Approved, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "SKU-1", null);
    }
}
