// <copyright file="SubmissionNotificationBroadcaster.cs" company="Henrik Jensen">
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

using System.Collections.Concurrent;
using System.Threading.Channels;
using Hj.EShop.SellerPortal.Bff.Contracts;

namespace Hj.EShop.SellerPortal.Bff.Notifications;

// See doc/CHRONICLE.md - "Rejected alternatives" - for why this is SSE fan-out, not
// SignalR: single-process, no replay, no cross-instance fan-out.
internal sealed class SubmissionNotificationBroadcaster
{
    private readonly ConcurrentDictionary<Guid, List<Channel<SubmissionSummary>>> _subscribersBySellerId = new();

    public Channel<SubmissionSummary> Subscribe(Guid sellerId)
    {
        var channel = Channel.CreateUnbounded<SubmissionSummary>();

        while (true)
        {
            List<Channel<SubmissionSummary>> subscribers = _subscribersBySellerId.GetOrAdd(sellerId, static _ => []);
            lock (subscribers)
            {
                // Unsubscribe emptied and dropped this exact list from the dictionary
                // between GetOrAdd returning it and us acquiring the lock - both sides
                // synchronize on the list instance, so this check is atomic with that
                // removal. Retrying with a fresh GetOrAdd avoids adding to an orphaned
                // list nobody will ever look up (and Publish would never find) again.
                if (!_subscribersBySellerId.TryGetValue(sellerId, out List<Channel<SubmissionSummary>>? current)
                    || !ReferenceEquals(current, subscribers))
                {
                    continue;
                }

                subscribers.Add(channel);
                return channel;
            }
        }
    }

    public void Unsubscribe(Guid sellerId, Channel<SubmissionSummary> channel)
    {
        if (!_subscribersBySellerId.TryGetValue(sellerId, out List<Channel<SubmissionSummary>>? subscribers))
        {
            return;
        }

        lock (subscribers)
        {
            subscribers.Remove(channel);
            if (subscribers.Count == 0)
            {
                // Drops the now-empty entry so a Seller who never reconnects doesn't
                // leak a dictionary entry forever.
                _subscribersBySellerId.TryRemove(sellerId, out _);
            }
        }
    }

    public void Publish(Guid sellerId, SubmissionSummary summary)
    {
        if (!_subscribersBySellerId.TryGetValue(sellerId, out List<Channel<SubmissionSummary>>? subscribers))
        {
            return;
        }

        Channel<SubmissionSummary>[] snapshot;
        lock (subscribers)
        {
            snapshot = [.. subscribers];
        }

        foreach (Channel<SubmissionSummary> channel in snapshot)
        {
            channel.Writer.TryWrite(summary);
        }
    }

    public int SubscriberCount(Guid sellerId)
    {
        if (!_subscribersBySellerId.TryGetValue(sellerId, out List<Channel<SubmissionSummary>>? subscribers))
        {
            return 0;
        }

        // Same lock Subscribe/Unsubscribe/Publish use - avoids observing a torn
        // intermediate state (e.g. mid-Add) while another thread mutates the list.
        lock (subscribers)
        {
            return subscribers.Count;
        }
    }
}
