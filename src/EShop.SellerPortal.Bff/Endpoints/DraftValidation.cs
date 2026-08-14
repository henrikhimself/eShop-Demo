// <copyright file="DraftValidation.cs" company="Henrik Jensen">
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

using Hj.EShop.SellerPortal.Bff.Contracts;

namespace Hj.EShop.SellerPortal.Bff.Endpoints;

// Shared sanity checks for movie/merchandise draft PUT requests. System.Text.Json
// does not honour non-nullable reference annotations at deserialization time - a
// missing JSON member still binds as null - so these checks must run explicitly
// before any field is copied onto the tracked entity.
internal static class DraftValidation
{
    private const int MaxShortTextLength = 200;
    private const int MaxDescriptionLength = 4000;
    private const int MaxFormatVariants = 20;

    // 1888 is the release year of the earliest surviving motion picture - anything
    // before that is certainly bad input, not a real edge case worth accommodating.
    private const int MinYearOfRelease = 1888;

    private static int MaxYearOfRelease => DateTime.UtcNow.Year + 5;

    public static IResult? ValidateMovieDraft(UpdateMovieDraftRequest request)
    {
        if (request.FormatVariants is null || request.FormatVariants.Count > MaxFormatVariants)
        {
            return Results.BadRequest($"Format variants must be a list of at most {MaxFormatVariants} items.");
        }

        if (IsMissingOrTooLong(request.Title, MaxShortTextLength))
        {
            return Results.BadRequest($"Title is required and must be at most {MaxShortTextLength} characters.");
        }

        if (IsMissingOrTooLong(request.Genre, MaxShortTextLength))
        {
            return Results.BadRequest($"Genre is required and must be at most {MaxShortTextLength} characters.");
        }

        if (request.Description is null || request.Description.Length > MaxDescriptionLength)
        {
            return Results.BadRequest($"Description must be at most {MaxDescriptionLength} characters.");
        }

        if (request.YearOfRelease < MinYearOfRelease || request.YearOfRelease > MaxYearOfRelease)
        {
            return Results.BadRequest($"Year of release must be between {MinYearOfRelease} and {MaxYearOfRelease}.");
        }

        foreach (UpdateFormatVariantRequest variant in request.FormatVariants)
        {
            if (variant.Price < 0)
            {
                return Results.BadRequest("Format variant price must not be negative.");
            }
        }

        return null;
    }

    public static IResult? ValidateMerchandiseDraft(UpdateMerchandiseDraftRequest request)
    {
        if (IsMissingOrTooLong(request.ProductName, MaxShortTextLength))
        {
            return Results.BadRequest($"Product name is required and must be at most {MaxShortTextLength} characters.");
        }

        if (request.Description is null || request.Description.Length > MaxDescriptionLength)
        {
            return Results.BadRequest($"Description must be at most {MaxDescriptionLength} characters.");
        }

        if (request.Price < 0)
        {
            return Results.BadRequest("Price must not be negative.");
        }

        if (request.AssociatedMovieTitle is { Length: > 0 } title && title.Length > MaxShortTextLength)
        {
            return Results.BadRequest($"Associated movie title must be at most {MaxShortTextLength} characters.");
        }

        return null;
    }

    private static bool IsMissingOrTooLong(string? value, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value) || value.Length > maxLength;
    }
}
