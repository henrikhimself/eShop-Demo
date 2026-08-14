// <copyright file="OidcTokenPruning.cs" company="Henrik Jensen">
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

namespace Hj.EShop.SellerPortal.Bff.Authentication;

// SaveTokens = true (Program.cs) stores every OIDC token onto the ticket by default,
// but nothing in this app ever reads access_token back. This class exists to shrink
// what actually gets persisted per Seller by stripping it out again. Wired into
// OnTicketReceived, which runs after OpenIdConnectHandler's own access_token protocol
// validation (ValidateTokenResponse) has already completed, so stripping it here can
// never break sign-in. id_token is kept: OpenIdConnectHandler's own sign-out flow
// reads it back from these same properties to set id_token_hint on the end-session
// redirect, which Keycloak requires - stripping it breaks logout.
internal static class OidcTokenPruning
{
    private static readonly string[] _tokenNamesToStrip = ["access_token"];

    public static void StripUnusedTokens(AuthenticationProperties properties)
    {
        properties.StoreTokens(properties.GetTokens().Where(token => !_tokenNamesToStrip.Contains(token.Name)));
    }
}
