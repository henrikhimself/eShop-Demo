// <copyright file="DraftEndpoints.cs" company="Henrik Jensen">
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

using Azure.Messaging.ServiceBus;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Hj.EShop.SellerPortal.Bff.Endpoints;

// Movie drafts only - see MerchandiseDraftEndpoints for the Merchandise CRUD routes,
// which share the same Draft base and UI patterns (see doc/SPEC.md).
internal static class DraftEndpoints
{
    public static IEndpointRouteBuilder MapDraftEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/bff/api/drafts", async (HttpContext context, SellerPortalDbContext db, CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            List<DraftSummary> drafts = await db.Drafts
                .Where(d => d.SellerId == seller.Id && d.Status != DraftStatus.PendingImageCleanup)
                .Select(d => new DraftSummary(
                    d.Id,
                    d.Status,
                    d is MovieDraft ? DraftKind.Movie : DraftKind.Merchandise,
                    d is MovieDraft ? ((MovieDraft)d).Title : ((MerchandiseDraft)d).ProductName,
                    d.LastRejectionReason))
                .ToListAsync(cancellationToken);

            return Results.Ok(drafts);
        })
        .RequireAuthorization("SellerOnly")
        .Produces<IReadOnlyList<DraftSummary>>(StatusCodes.Status200OK);

        endpoints.MapPost("/bff/api/drafts/movies", async (HttpContext context, SellerPortalDbContext db, CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MovieDraft draft = new()
            {
                Id = Guid.CreateVersion7(),
                SellerId = seller.Id,
                Title = string.Empty,
                Genre = string.Empty,
                Description = string.Empty,
                YearOfRelease = DateTime.UtcNow.Year,
                Status = DraftStatus.Draft,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };

            db.Drafts.Add(draft);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Created($"/bff/api/drafts/movies/{draft.Id}", ToDetail(draft));
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces<MovieDraftDetail>(StatusCodes.Status201Created);

        endpoints.MapGet("/bff/api/drafts/movies/{id:guid}", async (
            Guid id, HttpContext context, SellerPortalDbContext db, CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MovieDraft? draft = await FindMovieDraftAsync(db, id, seller.Id, cancellationToken);

            return draft is null ? Results.NotFound() : Results.Ok(ToDetail(draft));
        })
        .RequireAuthorization("SellerOnly")
        .Produces<MovieDraftDetail>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPut("/bff/api/drafts/movies/{id:guid}", async (
            Guid id,
            UpdateMovieDraftRequest request,
            HttpContext context,
            SellerPortalDbContext db,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MovieDraft? draft = await FindMovieDraftAsync(db, id, seller.Id, cancellationToken);
            if (draft is null)
            {
                return Results.NotFound();
            }

            if (draft.Status != DraftStatus.Draft)
            {
                return Results.Conflict();
            }

            if (DraftValidation.ValidateMovieDraft(request) is { } validationError)
            {
                return validationError;
            }

            if (ReconcileFormatVariants(draft, request.FormatVariants) is { } reconcileError)
            {
                return reconcileError;
            }

            draft.Title = request.Title;
            draft.Genre = request.Genre;
            draft.Description = request.Description;
            draft.YearOfRelease = request.YearOfRelease;
            draft.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToDetail(draft));
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces<MovieDraftDetail>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status400BadRequest);

        endpoints.MapDelete("/bff/api/drafts/movies/{id:guid}", async (
            Guid id,
            HttpContext context,
            SellerPortalDbContext db,
            ServiceBusClient serviceBusClient,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MovieDraft? draft = await FindMovieDraftAsync(db, id, seller.Id, cancellationToken);
            if (draft is null)
            {
                return Results.NotFound();
            }

            if (draft.Status != DraftStatus.Draft)
            {
                return Results.Conflict();
            }

            // Commit the whole Draft's removal before the actual blob deletes: a crash
            // here only ever leaves orphaned blobs for SubmissionImageDeletionConsumer
            // to clean up, never a dangling DB reference to a blob already gone.
            List<DraftImage> images = [.. draft.Images];
            db.Drafts.Remove(draft);
            await db.SaveChangesAsync(cancellationToken);

            await DraftImageCleanup.PublishDeletionMessagesAsync(serviceBusClient, draft.Id, images, cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return endpoints;
    }

    // AsSplitQuery: MovieDraft has two collection navigations (Images and
    // FormatVariants) - a single combined query for both trips EF Core's own
    // "cartesian explosion" warning and, worse, can corrupt change tracking for
    // subsequent adds to either collection.
    internal static Task<MovieDraft?> FindMovieDraftAsync(
        SellerPortalDbContext db, Guid draftId, Guid sellerId, CancellationToken cancellationToken)
    {
        return db.Drafts.OfType<MovieDraft>()
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == draftId && d.SellerId == sellerId, cancellationToken);
    }

    private static MovieDraftDetail ToDetail(MovieDraft draft)
    {
        DraftImage? image = draft.Images.SingleOrDefault();

        return new MovieDraftDetail(
            draft.Id,
            draft.Status,
            draft.Title,
            draft.Genre,
            draft.Description,
            draft.YearOfRelease,
            draft.LastRejectionReason,
            draft.FormatVariants.Select(v => new FormatVariantDto(v.Id, v.Format, v.Price)).ToList(),
            image is null ? null : new DraftImageDto(image.Id, $"/bff/api/drafts/movies/{draft.Id}/images/{image.Id}"));
    }

    // Returns a 404 result (without mutating draft.FormatVariants) if a requested
    // variant Id doesn't belong to this draft, instead of letting Single() throw
    // an unhandled InvalidOperationException (500).
    private static IResult? ReconcileFormatVariants(MovieDraft draft, IReadOnlyList<UpdateFormatVariantRequest> requestedVariants)
    {
        foreach (UpdateFormatVariantRequest requested in requestedVariants)
        {
            if (requested.Id is Guid variantId && draft.FormatVariants.All(v => v.Id != variantId))
            {
                return Results.NotFound($"Format variant '{variantId}' does not belong to this draft.");
            }
        }

        HashSet<Guid> requestedIds = [.. requestedVariants.Where(v => v.Id is not null).Select(v => v.Id!.Value)];
        draft.FormatVariants.RemoveAll(v => !requestedIds.Contains(v.Id));

        foreach (UpdateFormatVariantRequest requested in requestedVariants)
        {
            if (requested.Id is Guid variantId)
            {
                DraftFormatVariant existing = draft.FormatVariants.Single(v => v.Id == variantId);
                existing.Format = requested.Format;
                existing.Price = requested.Price;
            }
            else
            {
                draft.FormatVariants.Add(new DraftFormatVariant
                {
                    Id = Guid.CreateVersion7(),
                    DraftId = draft.Id,
                    Format = requested.Format,
                    Price = requested.Price,
                });
            }
        }

        return null;
    }
}
