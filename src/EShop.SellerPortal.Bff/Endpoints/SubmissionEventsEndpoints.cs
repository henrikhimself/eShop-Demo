// <copyright file="SubmissionEventsEndpoints.cs" company="Henrik Jensen">
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
using System.Threading.Channels;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Hj.EShop.SellerPortal.Bff.Endpoints;

internal static class SubmissionEventsEndpoints
{
    // Conservative choice - Kestrel's own default KeepAliveTimeout is 130s and nothing
    // else in this topology imposes a tighter idle timeout.
    private static readonly TimeSpan _keepAliveInterval = TimeSpan.FromSeconds(15);

    public static IEndpointRouteBuilder MapSubmissionEventsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/bff/api/submissions/events", async (
            HttpContext context,
            SellerPortalDbContext db,
            SubmissionNotificationBroadcaster broadcaster,
            IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            // Must pass the app's JsonOptions explicitly (unlike Results.Ok/Json) or
            // JsonSerializer.Serialize falls back to PascalCase, breaking the frontend's
            // camelCase field lookups.
            JsonSerializerOptions serializerOptions = jsonOptions.Value.SerializerOptions;

            Seller seller = await SellerProvisioner.EnsureSellerAsync(context.User, db, cancellationToken);

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";

            Channel<SubmissionSummary> channel = broadcaster.Subscribe(seller.Id);
            try
            {
                // An immediate comment, flushed right away: confirms the connection is
                // open without a client (real EventSource, or a test awaiting
                // ResponseHeadersRead) having to wait for the first real event or the
                // 15s keep-alive.
                await context.Response.WriteAsync(": connected\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);

                // Created once, outside the loop: re-creating it on every keep-alive
                // tick before the previous call completes would leak a waiter per tick.
                Task<bool> waitToRead = channel.Reader.WaitToReadAsync(cancellationToken).AsTask();

                while (!cancellationToken.IsCancellationRequested)
                {
                    Task completed = await Task.WhenAny(waitToRead, Task.Delay(_keepAliveInterval, cancellationToken));

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (completed == waitToRead)
                    {
                        bool hasData = waitToRead.Result;

                        // The previous waiter is now consumed - only now is it safe to
                        // register the next one, so at most one is ever outstanding.
                        waitToRead = channel.Reader.WaitToReadAsync(cancellationToken).AsTask();

                        if (hasData)
                        {
                            while (channel.Reader.TryRead(out SubmissionSummary? summary))
                            {
                                if (summary is null)
                                {
                                    continue;
                                }

                                await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(summary, serializerOptions)}\n\n", cancellationToken);
                            }
                        }
                        else
                        {
                            await context.Response.WriteAsync(": keep-alive\n\n", cancellationToken);
                        }
                    }
                    else
                    {
                        // Timed out waiting for data - waitToRead is still pending and
                        // is reused as-is on the next iteration, not replaced.
                        await context.Response.WriteAsync(": keep-alive\n\n", cancellationToken);
                    }

                    await context.Response.Body.FlushAsync(cancellationToken);
                }
            }
            finally
            {
                broadcaster.Unsubscribe(seller.Id, channel);
            }
        })
        .RequireAuthorization("SellerOnly")
        // Server-Sent Events - OpenAPI has no SSE representation, and SubmissionSummary's
        // schema is already covered by GET /bff/api/submissions (see
        // doc/adr/0014-bff-openapi-source-of-truth-for-frontend-types.md).
        .ExcludeFromDescription();

        return endpoints;
    }
}
