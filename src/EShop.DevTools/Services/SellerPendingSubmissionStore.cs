// <copyright file="SellerPendingSubmissionStore.cs" company="Henrik Jensen">
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
using System.Diagnostics.CodeAnalysis;
using Hj.EShop.Messaging;

namespace Hj.EShop.DevTools.Services;

// The simulator's in-memory view of "seller-submissions": SellerDraftApprovalSimulatorConsumer
// adds an entry as it drains the queue, the tool page lists what's here, and Approve/
// Reject removes an entry after publishing the outcome.
internal sealed class SellerPendingSubmissionStore
{
    // Keyed by SubmissionId. Pairs the message with the time this store received it
    // (not carried by SubmissionRequestMessage itself) so the tool page has a
    // submitted-at column to show.
    private readonly ConcurrentDictionary<Guid, SellerPendingSubmission> _submissions = new();

    public void Add(SubmissionRequestMessage message)
    {
        _submissions[message.SubmissionId] = new SellerPendingSubmission(message, DateTimeOffset.UtcNow);
    }

    public bool TryGet(Guid submissionId, [NotNullWhen(true)] out SellerPendingSubmission? submission)
    {
        return _submissions.TryGetValue(submissionId, out submission);
    }

    public bool Remove(Guid submissionId)
    {
        return _submissions.TryRemove(submissionId, out _);
    }

    public IReadOnlyCollection<SellerPendingSubmission> GetAll()
    {
        return [.. _submissions.Values.OrderBy(s => s.ReceivedAtUtc)];
    }
}

internal sealed record SellerPendingSubmission(SubmissionRequestMessage Message, DateTimeOffset ReceivedAtUtc);
