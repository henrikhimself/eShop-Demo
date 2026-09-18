// <copyright file="SubmissionEndpoints.cs" company="Henrik Jensen">
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

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Hj.EShop.Common;
using Hj.EShop.Messaging;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace Hj.EShop.SellerPortal.Bff.Endpoints;

internal static class SubmissionEndpoints
{
    public static IEndpointRouteBuilder MapSubmissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/bff/api/drafts/movies/{id:guid}/submit", async (
            Guid id,
            HttpContext context,
            SellerPortalDbContext db,
            ServiceBusClient serviceBusClient,
            SubmissionNotificationBroadcaster broadcaster,
            ILogger<Program> logger,
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

            if (draft.Images.Count != 1)
            {
                return Results.BadRequest("A movie draft needs exactly one cover image before it can be submitted.");
            }

            if (draft.FormatVariants.Count == 0)
            {
                return Results.BadRequest("A movie draft needs at least one format variant before it can be submitted.");
            }

            DraftImage coverImage = draft.Images[0];
            Submission submission = new()
            {
                Id = Guid.CreateVersion7(),
                SellerId = seller.Id,
                DraftId = draft.Id,
                TitleSnapshot = draft.Title,
                KindSnapshot = DraftKind.Movie,
                SubmittedAtUtc = DateTimeOffset.UtcNow,
                Status = SubmissionStatus.Pending,
            };
            db.Submissions.Add(submission);
            draft.Status = DraftStatus.PendingReview;

            // Commits before publish, not after - same crash-safety ordering
            // SubmissionResultConsumer already uses.
            await db.SaveChangesAsync(cancellationToken);

            SubmissionRequestMessage message = BuildMovieRequestMessage(submission, draft, coverImage);

            if (await PublishSubmissionRequestAsync(message, serviceBusClient, logger, submission.Id, cancellationToken) is { } errorResult)
            {
                return errorResult;
            }

            SubmissionSummary summary = ToSummary(submission);

            // Lets any other open connection for this Seller (e.g. a second tab)
            // learn about the new submission live - same rationale as
            // CancelReviewAsync's broadcast below.
            broadcaster.Publish(submission.SellerId, summary);

            return Results.Ok(summary);
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces<SubmissionSummary>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);

        endpoints.MapPost("/bff/api/drafts/merchandise/{id:guid}/submit", async (
            Guid id,
            HttpContext context,
            SellerPortalDbContext db,
            ServiceBusClient serviceBusClient,
            SubmissionNotificationBroadcaster broadcaster,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MerchandiseDraft? draft = await MerchandiseDraftEndpoints.FindMerchandiseDraftAsync(db, id, seller.Id, cancellationToken);
            if (draft is null)
            {
                return Results.NotFound();
            }

            if (draft.Status != DraftStatus.Draft)
            {
                return Results.Conflict();
            }

            // No image-count validation: SPEC.md sets no minimum for merchandise,
            // unlike a movie's required single cover image.
            Submission submission = new()
            {
                Id = Guid.CreateVersion7(),
                SellerId = seller.Id,
                DraftId = draft.Id,
                TitleSnapshot = draft.ProductName,
                KindSnapshot = DraftKind.Merchandise,
                SubmittedAtUtc = DateTimeOffset.UtcNow,
                Status = SubmissionStatus.Pending,
            };
            db.Submissions.Add(submission);
            draft.Status = DraftStatus.PendingReview;

            // Same ordering as above.
            await db.SaveChangesAsync(cancellationToken);

            SubmissionRequestMessage message = BuildMerchandiseRequestMessage(submission, draft);

            if (await PublishSubmissionRequestAsync(message, serviceBusClient, logger, submission.Id, cancellationToken) is { } errorResult)
            {
                return errorResult;
            }

            SubmissionSummary summary = ToSummary(submission);

            // Same rationale as the movie submit route's broadcast above.
            broadcaster.Publish(submission.SellerId, summary);

            return Results.Ok(summary);
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces<SubmissionSummary>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status500InternalServerError);

        endpoints.MapPost("/bff/api/drafts/movies/{id:guid}/cancel-review", async (
            Guid id,
            HttpContext context,
            SellerPortalDbContext db,
            ServiceBusClient serviceBusClient,
            SubmissionNotificationBroadcaster broadcaster,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MovieDraft? draft = await DraftEndpoints.FindMovieDraftAsync(db, id, seller.Id, cancellationToken);
            return await CancelReviewAsync(db, draft, serviceBusClient, broadcaster, logger, cancellationToken);
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status500InternalServerError);

        endpoints.MapPost("/bff/api/drafts/merchandise/{id:guid}/cancel-review", async (
            Guid id,
            HttpContext context,
            SellerPortalDbContext db,
            ServiceBusClient serviceBusClient,
            SubmissionNotificationBroadcaster broadcaster,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            MerchandiseDraft? draft = await MerchandiseDraftEndpoints.FindMerchandiseDraftAsync(db, id, seller.Id, cancellationToken);
            return await CancelReviewAsync(db, draft, serviceBusClient, broadcaster, logger, cancellationToken);
        })
        .RequireAuthorization("SellerOnly")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status500InternalServerError);

        endpoints.MapGet("/bff/api/submissions", async (HttpContext context, SellerPortalDbContext db, CancellationToken cancellationToken) =>
        {
            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            // Ordered client-side, not via OrderBy in the query: SQLite (used in tests)
            // cannot order by DateTimeOffset in SQL. Fine at this app's per-seller
            // submission volume.
            List<SubmissionSummary> submissions = await db.Submissions
                .Where(s => s.SellerId == seller.Id)
                .Select(s => new SubmissionSummary(
                    s.Id, s.TitleSnapshot, s.Status, s.SubmittedAtUtc, s.RespondedAtUtc, s.AssignedSku, s.RejectionReason))
                .ToListAsync(cancellationToken);
            submissions = [.. submissions.OrderByDescending(s => s.SubmittedAtUtc)];

            return Results.Ok(submissions);
        })
        .RequireAuthorization("SellerOnly")
        .Produces<IReadOnlyList<SubmissionSummary>>(StatusCodes.Status200OK);

        return endpoints;
    }

    // Shared by both cancel-review routes: the Seller withdraws a draft from review,
    // letting them resume editing and eventually resubmit (a brand new Submission -
    // see SPEC.md's Submission workflow). Cancelling the Submission row here, rather
    // than deleting it, keeps its history visible on the Seller's submissions list and
    // lets SubmissionResultConsumer's Pending-status guard recognize and safely ignore
    // a late Approve/Reject result for it.
    private static async Task<IResult> CancelReviewAsync(
        SellerPortalDbContext db,
        Draft? draft,
        ServiceBusClient serviceBusClient,
        SubmissionNotificationBroadcaster broadcaster,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (draft is null)
        {
            return Results.NotFound();
        }

        if (draft.Status != DraftStatus.PendingReview)
        {
            return Results.Conflict();
        }

        Submission? submission = await db.Submissions
            .Where(s => s.DraftId == draft.Id && s.Status == SubmissionStatus.Pending)
            .SingleOrDefaultAsync(cancellationToken);
        if (submission is null)
        {
            // Should not happen: a PendingReview draft always has a matching Pending
            // submission. Defensive, not expected.
            return Results.Conflict();
        }

        submission.Status = SubmissionStatus.Cancelled;
        submission.RespondedAtUtc = DateTimeOffset.UtcNow;
        draft.Status = DraftStatus.Draft;

        // Commits before publish, not after - same crash-safety ordering the submit
        // routes already use.
        await db.SaveChangesAsync(cancellationToken);

        if (await PublishCancellationAsync(submission.Id, serviceBusClient, logger, cancellationToken) is { } errorResult)
        {
            return errorResult;
        }

        // Lets any other open connection for this Seller (e.g. a second tab, or an
        // open edit page's own SSE listener) learn about the cancellation live -
        // separate from the toast the Seller who clicked the button already gets
        // synchronously from this request's own response.
        broadcaster.Publish(submission.SellerId, ToSummary(submission));

        return Results.NoContent();
    }

    private static SubmissionRequestMessage BuildMovieRequestMessage(Submission submission, MovieDraft draft, DraftImage coverImage)
    {
        return new(
            submission.Id,
            submission.SellerId,
            SubmissionKind.Movie,
            draft.Title,
            draft.Description,
            draft.Genre,
            draft.YearOfRelease,
            draft.FormatVariants.Select(v => new SubmissionFormatVariantPayload(v.Format.ToString(), v.Price)).ToList(),
            AssociatedMovieTitle: null,
            ImageBlobReferences: [coverImage.BlobReference],
            Price: null);
    }

    private static SubmissionRequestMessage BuildMerchandiseRequestMessage(Submission submission, MerchandiseDraft draft)
    {
        return new(
            submission.Id,
            submission.SellerId,
            SubmissionKind.Merchandise,
            draft.ProductName,
            draft.Description,
            Genre: null,
            YearOfRelease: null,
            FormatVariants: null,
            draft.AssociatedMovieTitle,
            draft.Images.OrderBy(i => i.Position).Select(i => i.BlobReference).ToList(),
            Price: draft.Price);
    }

    // Shared by both submit routes: publishes the request and turns a broker failure
    // into a 500, keeping the already-committed DB state as-is (see the crash-safety
    // comment above each call site). Returns null on success.
    private static async Task<IResult?> PublishSubmissionRequestAsync(
        SubmissionRequestMessage message,
        ServiceBusClient serviceBusClient,
        ILogger<Program> logger,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using ServiceBusSender sender = serviceBusClient.CreateSender(KnownNames.ResourceSellerSubmissions);
            await SendWithRetryAsync(
                sender, new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(message)), logger, submissionId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Any transport exception that survives SendWithRetryAsync's bounded retries
            // must hit this log-and-500 path; the `when` clause only lets a genuine
            // caller-initiated cancellation propagate normally. DB state above is already
            // durable - no compensating transaction exists yet for a persistent failure.
            logger.LogError(exception, "Failed to publish a submission request for submission {SubmissionId}.", submissionId);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        return null;
    }

    // Shared by both cancel-review routes: same publish-and-turn-broker-failure-into-a-
    // 500 pattern as PublishSubmissionRequestAsync above. Returns null on success.
    private static async Task<IResult?> PublishCancellationAsync(
        Guid submissionId,
        ServiceBusClient serviceBusClient,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await using ServiceBusSender sender = serviceBusClient.CreateSender(KnownNames.ResourceSellerSubmissionsCancellations);
            SubmissionCancelledMessage message = new(submissionId);
            await SendWithRetryAsync(
                sender, new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(message)), logger, submissionId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Same rationale as PublishSubmissionRequestAsync's catch clause above.
            logger.LogError(exception, "Failed to publish a submission cancellation for submission {SubmissionId}.", submissionId);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        return null;
    }

    // Shared by both publish helpers above: retries a transient broker failure (e.g.
    // Service Bus briefly unreachable) a bounded number of times before letting the
    // caller's catch turn a persistent failure into a 500. Same Polly idiom as
    // ServiceBusQueueConsumer's startup retry (EShop.Messaging), but bounded since this
    // runs inline inside an HTTP request instead of a background service.
    private static async Task SendWithRetryAsync(
        ServiceBusSender sender,
        ServiceBusMessage message,
        ILogger<Program> logger,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(exception => exception is not OperationCanceledException),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
                MaxDelay = TimeSpan.FromSeconds(1),
                MaxRetryAttempts = 2,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Retrying a submission-message publish for submission {SubmissionId} (attempt {AttemptNumber}).",
                        submissionId,
                        args.AttemptNumber + 1);
                    return default;
                },
            })
            .Build();

        await pipeline.ExecuteAsync(
            ct => new ValueTask(sender.SendMessagesAsync([message], ct)),
            cancellationToken);
    }

    internal static SubmissionSummary ToSummary(Submission submission)
    {
        return new SubmissionSummary(
            submission.Id,
            submission.TitleSnapshot,
            submission.Status,
            submission.SubmittedAtUtc,
            submission.RespondedAtUtc,
            submission.AssignedSku,
            submission.RejectionReason);
    }
}
