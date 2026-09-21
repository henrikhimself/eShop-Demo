// <copyright file="Draft.cs" company="Henrik Jensen">
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

// TPH base for in-progress Seller submissions. MovieDraft and MerchandiseDraft share
// these fields; product-specific fields live in each subclass.
internal abstract class Draft
{
    public required Guid Id { get; init; }

    public required Guid SellerId { get; init; }

    public Seller Seller { get; init; } = null!;

    public DraftStatus Status { get; set; } = DraftStatus.Draft;

    // Set when a linked Submission is rejected, so the Seller sees the reason while
    // editing.
    public string? LastRejectionReason { get; set; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    // Exactly 1 movie cover image, up to 3 merchandise images; enforced in application
    // logic, not as a database constraint.
    public List<DraftImage> Images { get; init; } = [];
}
