// <copyright file="SubmissionMessages.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Messaging;

// See doc/CHRONICLE.md — payload shape is a placeholder pending a real Commerce Connect consumer (ADR 0009).
public sealed record SubmissionResultMessage(
    Guid SubmissionId,
    bool Approved,
    string? AssignedSku,
    string? RejectionReason);

// Kind-agnostic: movie-only fields (Genre, YearOfRelease, FormatVariants) are null for a
// Merchandise submission; merchandise-only fields (AssociatedMovieTitle, Price) are null
// for a Movie submission.
public sealed record SubmissionRequestMessage(
    Guid SubmissionId,
    Guid SellerId,
    SubmissionKind Kind,
    string TitleSnapshot,
    string Description,
    string? Genre,
    int? YearOfRelease,
    IReadOnlyList<SubmissionFormatVariantPayload>? FormatVariants,
    string? AssociatedMovieTitle,
    IReadOnlyList<string> ImageBlobReferences,
    decimal? Price);

public sealed record SubmissionFormatVariantPayload(string Format, decimal Price);

public sealed record SubmissionCancelledMessage(Guid SubmissionId);

// A consumer-facing kind, distinct from the Bff project's own internal DraftKind - a
// message contract can't reference a type from across the assembly boundary.
public enum SubmissionKind
{
    Movie,
    Merchandise,
}
