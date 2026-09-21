// <copyright file="AntiforgeryEndpoints.cs" company="Henrik Jensen">
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

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Antiforgery;

namespace Hj.EShop.SellerPortal.Bff.Endpoints;

internal static class AntiforgeryEndpoints
{
    public static IEndpointRouteBuilder MapAntiforgeryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/bff/api/antiforgery/token", IssueToken)
            .RequireAuthorization("SellerOnly")
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    // Standard SPA pattern: the Next.js app reads this non-HttpOnly cookie and echoes it
    // as X-XSRF-TOKEN on every mutating request (see lib/bff-fetch.ts). Secure mirrors
    // the request scheme, matching the auth cookie's own policy for local HTTP dev.
    [SuppressMessage("Security", "S3330", Justification = "Deliberately not HttpOnly - client-side JS must read this cookie.")]
    [SuppressMessage("Security", "S2092", Justification = "Secure mirrors the request scheme for local HTTP dev - see comment above.")]
    private static IResult IssueToken(HttpContext context, IAntiforgery antiforgery)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
        string requestToken = tokens.RequestToken
            ?? throw new InvalidOperationException("Antiforgery did not produce a request token.");

        context.Response.Cookies.Append("XSRF-TOKEN", requestToken, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
        });

        return Results.NoContent();
    }
}
