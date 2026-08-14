// <copyright file="MerchandiseDraftEndpoints.cs" company="Henrik Jensen">
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

// Merchandise drafts only - see DraftEndpoints for the shared list route and the movie
// CRUD routes.
internal static class MerchandiseDraftEndpoints
{
    public static IEndpointRouteBuilder MapMerchandiseDraftEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/bff/api/drafts/merchandise", async (HttpContext context, SellerPortalDbContext db, CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MerchandiseDraft draft = new()
            {
                Id = Guid.CreateVersion7(),
                SellerId = seller.Id,
                ProductName = string.Empty,
                Description = string.Empty,
                Price = 0,
                AssociatedMovieTitle = null,
                Status = DraftStatus.Draft,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };

            db.Drafts.Add(draft);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Created($"/bff/api/drafts/merchandise/{draft.Id}", ToDetail(draft));
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces<MerchandiseDraftDetail>(StatusCodes.Status201Created);

        endpoints.MapGet("/bff/api/drafts/merchandise/{id:guid}", async (
            Guid id, HttpContext context, SellerPortalDbContext db, CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MerchandiseDraft? draft = await FindMerchandiseDraftAsync(db, id, seller.Id, cancellationToken);

            return draft is null ? Results.NotFound() : Results.Ok(ToDetail(draft));
        })
        .RequireAuthorization("SellerOnly")
        .Produces<MerchandiseDraftDetail>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPut("/bff/api/drafts/merchandise/{id:guid}", async (
            Guid id,
            UpdateMerchandiseDraftRequest request,
            HttpContext context,
            SellerPortalDbContext db,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MerchandiseDraft? draft = await FindMerchandiseDraftAsync(db, id, seller.Id, cancellationToken);
            if (draft is null)
            {
                return Results.NotFound();
            }

            if (draft.Status != DraftStatus.Draft)
            {
                return Results.Conflict();
            }

            if (DraftValidation.ValidateMerchandiseDraft(request) is { } validationError)
            {
                return validationError;
            }

            draft.ProductName = request.ProductName;
            draft.Description = request.Description;
            draft.Price = request.Price;
            draft.AssociatedMovieTitle = request.AssociatedMovieTitle;
            draft.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToDetail(draft));
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces<MerchandiseDraftDetail>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status400BadRequest);

        endpoints.MapDelete("/bff/api/drafts/merchandise/{id:guid}", async (
            Guid id,
            HttpContext context,
            SellerPortalDbContext db,
            ServiceBusClient serviceBusClient,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MerchandiseDraft? draft = await FindMerchandiseDraftAsync(db, id, seller.Id, cancellationToken);
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

    // No AsSplitQuery, unlike FindMovieDraftAsync: MerchandiseDraft has only one
    // collection navigation (Images), so there is no second collection for a combined
    // query to cross-join against.
    internal static Task<MerchandiseDraft?> FindMerchandiseDraftAsync(
        SellerPortalDbContext db, Guid draftId, Guid sellerId, CancellationToken cancellationToken)
    {
        return db.Drafts.OfType<MerchandiseDraft>()
            .FirstOrDefaultAsync(d => d.Id == draftId && d.SellerId == sellerId, cancellationToken);
    }

    private static MerchandiseDraftDetail ToDetail(MerchandiseDraft draft)
    {
        return new MerchandiseDraftDetail(
            draft.Id,
            draft.Status,
            draft.ProductName,
            draft.Description,
            draft.Price,
            draft.AssociatedMovieTitle,
            draft.LastRejectionReason,
            draft.Images.OrderBy(i => i.Position)
                .Select(i => new DraftImageDto(i.Id, $"/bff/api/drafts/merchandise/{draft.Id}/images/{i.Id}"))
                .ToList());
    }
}
