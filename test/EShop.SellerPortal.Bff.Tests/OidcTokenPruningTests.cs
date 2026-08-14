// <copyright file="OidcTokenPruningTests.cs" company="Henrik Jensen">
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

using Hj.EShop.SellerPortal.Bff.Authentication;
using Microsoft.AspNetCore.Authentication;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// No WebApplicationFactory needed - TestAuthHandler bypasses the real OIDC handshake
// entirely (see CookieAuthenticationWiringTests), so this exercises the token-pruning
// logic directly against the same AuthenticationProperties.StoreTokens/GetTokens
// helpers OpenIdConnectHandler itself uses, rather than needing a real Keycloak
// round trip.
public sealed class OidcTokenPruningTests
{
    [Fact]
    public void StripUnusedTokens_RemovesAccessTokenButKeepsIdTokenRefreshTokenAndMetadata()
    {
        AuthenticationProperties properties = new();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "an-access-token" },
            new AuthenticationToken { Name = "id_token", Value = "an-id-token" },
            new AuthenticationToken { Name = "refresh_token", Value = "a-refresh-token" },
            new AuthenticationToken { Name = "token_type", Value = "Bearer" },
            new AuthenticationToken { Name = "expires_at", Value = "2026-01-01T00:00:00+00:00" },
        ]);

        OidcTokenPruning.StripUnusedTokens(properties);

        Assert.Null(properties.GetTokenValue("access_token"));
        Assert.Equal("an-id-token", properties.GetTokenValue("id_token"));
        Assert.Equal("a-refresh-token", properties.GetTokenValue("refresh_token"));
        Assert.Equal("Bearer", properties.GetTokenValue("token_type"));
        Assert.Equal("2026-01-01T00:00:00+00:00", properties.GetTokenValue("expires_at"));
    }

    [Fact]
    public void StripUnusedTokens_OnlyAccessToken_LeavesAnEmptyTokenSet()
    {
        AuthenticationProperties properties = new();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "an-access-token" },
        ]);

        OidcTokenPruning.StripUnusedTokens(properties);

        Assert.Empty(properties.GetTokens());
    }
}
