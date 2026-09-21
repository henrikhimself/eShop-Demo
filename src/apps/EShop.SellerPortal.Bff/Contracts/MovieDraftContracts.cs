// <copyright file="MovieDraftContracts.cs" company="Henrik Jensen">
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

using Hj.EShop.SellerPortal.Bff.Data.Entities;

namespace Hj.EShop.SellerPortal.Bff.Contracts;

internal sealed record DraftSummary(Guid Id, DraftStatus Status, DraftKind Kind, string Title, string? LastRejectionReason);

internal sealed record MovieDraftDetail(
    Guid Id,
    DraftStatus Status,
    string Title,
    string Genre,
    string Description,
    int YearOfRelease,
    string? LastRejectionReason,
    IReadOnlyList<FormatVariantDto> FormatVariants,
    DraftImageDto? CoverImage);

internal sealed record FormatVariantDto(Guid Id, MovieFormat Format, decimal Price);

// Url is the BFF-relative streaming path, never a raw blob URL - the browser never
// talks to blob storage directly (see DraftImageEndpoints).
internal sealed record DraftImageDto(Guid Id, string Url);

internal sealed record UpdateMovieDraftRequest(
    string Title,
    string Genre,
    string Description,
    int YearOfRelease,
    IReadOnlyList<UpdateFormatVariantRequest> FormatVariants);

// Id null means a new variant.
internal sealed record UpdateFormatVariantRequest(Guid? Id, MovieFormat Format, decimal Price);
