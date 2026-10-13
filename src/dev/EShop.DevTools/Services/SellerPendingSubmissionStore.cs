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

// In-memory view of "seller-submissions": SellerDraftApprovalSimulatorConsumer adds
// entries while draining the queue, the tool page lists them, and Approve/Reject removes
// them after publishing the outcome.
internal sealed class SellerPendingSubmissionStore
{
    private static readonly TimeSpan _cancellationRetention = TimeSpan.FromMinutes(10);

    // Keyed by SubmissionId; pairs the message with its receipt time (not present on
    // SubmissionRequestMessage) for the tool page's submitted-at column.
    private readonly ConcurrentDictionary<Guid, SellerPendingSubmission> _submissions = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _cancelledSubmissionExpiry = new();
    private readonly TimeProvider _timeProvider;

    public SellerPendingSubmissionStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool Add(SubmissionRequestMessage message)
    {
        PruneExpiredCancellations();

        if (_cancelledSubmissionExpiry.TryRemove(message.SubmissionId, out _))
        {
            return false;
        }

        return _submissions.TryAdd(message.SubmissionId, new SellerPendingSubmission(message, _timeProvider.GetUtcNow()));
    }

    public bool TryGet(Guid submissionId, [NotNullWhen(true)] out SellerPendingSubmission? submission)
    {
        return _submissions.TryGetValue(submissionId, out submission);
    }

    public SellerSubmissionCancellationResult Cancel(Guid submissionId)
    {
        PruneExpiredCancellations();

        if (_submissions.TryRemove(submissionId, out _))
        {
            return SellerSubmissionCancellationResult.Removed;
        }

        _cancelledSubmissionExpiry[submissionId] = _timeProvider.GetUtcNow().Add(_cancellationRetention);
        return SellerSubmissionCancellationResult.Remembered;
    }

    public bool Remove(Guid submissionId)
    {
        return _submissions.TryRemove(submissionId, out _);
    }

    public IReadOnlyCollection<SellerPendingSubmission> GetAll()
    {
        PruneExpiredCancellations();
        return [.. _submissions.Values.OrderBy(s => s.ReceivedAtUtc)];
    }

    private void PruneExpiredCancellations()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        foreach ((Guid submissionId, DateTimeOffset expiresAtUtc) in _cancelledSubmissionExpiry)
        {
            if (expiresAtUtc <= now)
            {
                _cancelledSubmissionExpiry.TryRemove(new KeyValuePair<Guid, DateTimeOffset>(submissionId, expiresAtUtc));
            }
        }
    }
}

internal enum SellerSubmissionCancellationResult
{
    Removed,
    Remembered,
}

internal sealed record SellerPendingSubmission(SubmissionRequestMessage Message, DateTimeOffset ReceivedAtUtc);
