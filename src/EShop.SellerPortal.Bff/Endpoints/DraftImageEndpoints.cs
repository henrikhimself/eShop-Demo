// <copyright file="DraftImageEndpoints.cs" company="Henrik Jensen">
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
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Messaging;

namespace Hj.EShop.SellerPortal.Bff.Endpoints;

// A movie draft has exactly one cover image (SPEC.md field list). See
// MerchandiseImageEndpoints for the up-to-3, add/delete-only image routes.
internal static class DraftImageEndpoints
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> _allowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public static IEndpointRouteBuilder MapDraftImageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/bff/api/drafts/movies/{id:guid}/images", async (
            Guid id,
            IFormFile file,
            HttpContext context,
            SellerPortalDbContext db,
            BlobContainerClient submissionsImageContainer,
            ServiceBusClient serviceBusClient,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MovieDraft? draft = await DraftEndpoints.FindMovieDraftAsync(db, id, seller.Id, cancellationToken);
            if (draft is null)
            {
                return Results.NotFound();
            }

            if (draft.Status != DraftStatus.Draft)
            {
                return Results.Conflict();
            }

            if (!_allowedContentTypes.Contains(file.ContentType) || file.Length > MaxImageSizeBytes)
            {
                return Results.BadRequest("The cover image must be JPEG, PNG, or WebP and no larger than 5 MB.");
            }

            // Upload replaces: a movie draft has exactly one cover image. Upload the
            // new blob and commit the DB swap before touching the old blob at all - a
            // crash here leaves the old blob and its now-superseded row both still in
            // place, an easy, fully consistent state to retry from.
            string blobReference = $"{draft.Id}/{Guid.CreateVersion7()}{Path.GetExtension(file.FileName)}";
            BlobClient blobClient = submissionsImageContainer.GetBlobClient(blobReference);
            await using (Stream stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(
                    stream,
                    new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType } },
                    cancellationToken);
            }

            DraftImage? existingImage = draft.Images.SingleOrDefault();
            if (existingImage is not null)
            {
                draft.Images.Remove(existingImage);
            }

            DraftImage image = new()
            {
                Id = Guid.CreateVersion7(),
                DraftId = draft.Id,
                BlobReference = blobReference,
                Position = 0,
            };
            draft.Images.Add(image);
            draft.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            // Only after the DB row swap is durable: the old blob is now orphaned at
            // worst, never dangling-referenced, if this publish itself never happens.
            if (existingImage is not null)
            {
                await DraftImageCleanup.PublishDeletionMessagesAsync(serviceBusClient, draft.Id, [existingImage], cancellationToken);
            }

            return Results.Ok(new DraftImageDto(image.Id, $"/bff/api/drafts/movies/{draft.Id}/images/{image.Id}"));
        })
        .RequireAuthorization("SellerOnly")
        .DisableAntiforgery()
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces<DraftImageDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status400BadRequest);

        // Streams through the BFF, echoing the blob's own content type, so
        // <img src="/bff/api/drafts/movies/{id}/images/{imageId}"> works through the
        // existing same-origin proxy with no SAS tokens or public blob access.
        endpoints.MapGet("/bff/api/drafts/movies/{id:guid}/images/{imageId:guid}", async (
            Guid id,
            Guid imageId,
            HttpContext context,
            SellerPortalDbContext db,
            BlobContainerClient submissionsImageContainer,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MovieDraft? draft = await DraftEndpoints.FindMovieDraftAsync(db, id, seller.Id, cancellationToken);
            DraftImage? image = draft?.Images.SingleOrDefault(i => i.Id == imageId);
            if (image is null)
            {
                return Results.NotFound();
            }

            BlobDownloadStreamingResult download = await submissionsImageContainer
                .GetBlobClient(image.BlobReference)
                .DownloadStreamingAsync(cancellationToken: cancellationToken);

            return Results.Stream(download.Content, download.Details.ContentType);
        })
        .RequireAuthorization("SellerOnly")
        .Produces(StatusCodes.Status404NotFound);

        endpoints.MapDelete("/bff/api/drafts/movies/{id:guid}/images/{imageId:guid}", async (
            Guid id,
            Guid imageId,
            HttpContext context,
            SellerPortalDbContext db,
            ServiceBusClient serviceBusClient,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MovieDraft? draft = await DraftEndpoints.FindMovieDraftAsync(db, id, seller.Id, cancellationToken);
            if (draft is null)
            {
                return Results.NotFound();
            }

            if (draft.Status != DraftStatus.Draft)
            {
                return Results.Conflict();
            }

            DraftImage? image = draft.Images.SingleOrDefault(i => i.Id == imageId);
            if (image is null)
            {
                return Results.NotFound();
            }

            // Commit the row removal before the actual blob delete: a crash here only
            // ever leaves an orphaned blob for SubmissionImageDeletionConsumer to clean
            // up, never a dangling DB reference to a blob already gone.
            draft.Images.Remove(image);
            draft.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            await DraftImageCleanup.PublishDeletionMessagesAsync(serviceBusClient, draft.Id, [image], cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
