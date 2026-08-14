// <copyright file="TestAuthHandler.cs" company="Henrik Jensen">
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

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// Stands in for the real Keycloak/OIDC handshake in tests: a request carrying the
// UserHeaderName header authenticates as that Keycloak subject id; a request without
// it is unauthenticated, exercising the same 401 path a real anonymous request hits.
// The optional RoleHeaderName header stands in for the "Seller" realm role a Site
// Administrator assigns in Keycloak (see Program.cs's "SellerOnly" policy).
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeaderName = "X-Test-User";
    public const string RoleHeaderName = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeaderName, out Microsoft.Extensions.Primitives.StringValues subjectId)
            || string.IsNullOrEmpty(subjectId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, subjectId!)];
        if (Request.Headers.TryGetValue(RoleHeaderName, out Microsoft.Extensions.Primitives.StringValues role)
            && !string.IsNullOrEmpty(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role!));
        }

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
