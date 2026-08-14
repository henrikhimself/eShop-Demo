// <copyright file="AuthEndpoints.cs" company="Henrik Jensen">
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

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Hj.EShop.SellerPortal.Bff.Endpoints;

// The Next.js app never talks to Keycloak or holds a token - it only ever navigates
// the browser to these same-origin (proxied) endpoints.
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/bff/login", () =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/drafts" },
                [OpenIdConnectDefaults.AuthenticationScheme]))
            .ExcludeFromDescription();

        endpoints.MapGet("/bff/logout", (HttpContext context) =>
        {
            // OnValidatePrincipal (AuthConfiguration.cs) already rejected this session
            // (or there never was one) - Keycloak has nothing left to end, and any
            // id_token_hint sent (stale, or now missing) just dead-ends on its own
            // error page instead of redirecting back. Sign out locally instead of
            // attempting that round trip - see doc/CHRONICLE.md.
            AuthenticationProperties properties = new() { RedirectUri = "/" };
            return context.User.Identity?.IsAuthenticated == true
                ? Results.SignOut(properties, [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme])
                : Results.SignOut(properties, [CookieAuthenticationDefaults.AuthenticationScheme]);
        })
            .ExcludeFromDescription();

        // Not one of the Contracts/*.cs data contracts this OpenAPI document exists to
        // unify (see doc/adr/0014-bff-openapi-source-of-truth-for-frontend-types.md).
        endpoints.MapGet("/bff/user", (HttpContext context) =>
            Results.Ok(context.User.Claims.Select(claim => new { claim.Type, claim.Value })))
            .RequireAuthorization()
            .ExcludeFromDescription();

        return endpoints;
    }
}
