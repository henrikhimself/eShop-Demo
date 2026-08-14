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

namespace Hj.EShop.StoreFront.Web.Authentication;

// Mirrors EShop.SellerPortal.Bff/Endpoints/AuthEndpoints.cs's /bff/login and /bff/logout
// pattern, without the "/bff/" prefix - Storefront is not proxied behind a separate
// frontend the way the Bff is.
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/login", () =>
            Results.Challenge(
                new AuthenticationProperties { RedirectUri = "/" },
                [OpenIdConnectDefaults.AuthenticationScheme]))
            .ExcludeFromDescription();

        endpoints.MapGet("/logout", (HttpContext context) =>
        {
            AuthenticationProperties properties = new() { RedirectUri = "/" };
            return context.User.Identity?.IsAuthenticated == true
                ? Results.SignOut(properties, [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme])
                : Results.SignOut(properties, [CookieAuthenticationDefaults.AuthenticationScheme]);
        })
            .ExcludeFromDescription();

        // Not user-facing - lets a caller (including this phase's own e2e login test)
        // confirm the claims a sign-in actually produced, same reasoning as the Bff's
        // own /bff/user.
        endpoints.MapGet("/user", (HttpContext context) =>
            Results.Ok(context.User.Claims.Select(claim => new { claim.Type, claim.Value })))
            .RequireAuthorization()
            .ExcludeFromDescription();

        return endpoints;
    }
}
