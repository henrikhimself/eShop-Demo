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

namespace Hj.EShop.TicketStore;

// SaveTokens stores every OIDC token by default, but access_token is never read back here - stripped to shrink what gets persisted.
// Wire into OnTicketReceived, which runs after OpenIdConnectHandler's own access_token validation has completed, so stripping it here can never break sign-in.
// id_token is deliberately kept: sign-out reads it back to set id_token_hint on the end-session redirect, which Keycloak requires - stripping it breaks logout.
public static class OidcTokenPruning
{
    private static readonly string[] _tokenNamesToStrip = ["access_token"];

    public static void StripUnusedTokens(AuthenticationProperties properties)
    {
        properties.StoreTokens(properties.GetTokens().Where(token => !_tokenNamesToStrip.Contains(token.Name)));
    }
}
