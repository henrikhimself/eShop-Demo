// <copyright file="Submission.cs" company="Henrik Jensen">
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

namespace Hj.EShop.SellerPortal.Bff.Data.Entities;

// Review-workflow state for a submitted draft, and the Seller's local record of
// submission outcomes (including assigned SKU when approved).
internal sealed class Submission
{
    public required Guid Id { get; init; }

    public required Guid SellerId { get; init; }

    // Nullable + SetNull: approved submissions survive draft deletion, so snapshot
    // fields below keep this row meaningful.
    public Guid? DraftId { get; set; }

    public required string TitleSnapshot { get; init; }

    public required DraftKind KindSnapshot { get; init; }

    public required DateTimeOffset SubmittedAtUtc { get; init; }

    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;

    // Placeholder from the deduplication step; not a real SKU catalog reference yet.
    public string? ProposedSku { get; set; }

    // Set on approval and returned via seller-submissions-result. String, not FK:
    // there is no local SKU catalog.
    public string? AssignedSku { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset? RespondedAtUtc { get; set; }
}
