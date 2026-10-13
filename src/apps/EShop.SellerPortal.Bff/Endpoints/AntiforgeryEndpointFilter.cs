// <copyright file="AntiforgeryEndpointFilter.cs" company="Henrik Jensen">
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

using Microsoft.AspNetCore.Antiforgery;

namespace Hj.EShop.SellerPortal.Bff.Endpoints;

// ASP.NET Core's automatic antiforgery check only fires for [FromForm]/IFormFile
// minimal-API parameters, and even there a failure only logs in Production instead of
// blocking the request. Every mutating draft/image/submission route applies this filter
// explicitly instead (the multipart upload route also needs .DisableAntiforgery(), so
// its own toothless automatic check never runs).
internal sealed class AntiforgeryEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    // Lets lib/bff-fetch.ts retry once after a stale-CSRF-pairing failure - a header,
    // not a status code, since 400/403 are already used by other failure modes a
    // retry can't fix.
    internal const string InvalidHeaderName = "X-Antiforgery-Invalid";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.HttpContext.Response.Headers[InvalidHeaderName] = "true";
            return Results.BadRequest("The antiforgery token is missing or invalid.");
        }

        return await next(context);
    }
}
