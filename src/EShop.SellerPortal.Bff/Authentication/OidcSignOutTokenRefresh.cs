// <copyright file="OidcSignOutTokenRefresh.cs" company="Henrik Jensen">
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

using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;

namespace Hj.EShop.SellerPortal.Bff.Authentication;

// Refreshes or rejects id_token_hint before sign-out. Keycloak restarts can invalidate
// a token without changing its exp claim, so Keycloak mode can force provider
// confirmation instead of trusting IsExpired alone.
internal static class OidcSignOutTokenRefresh
{
    public static bool IsExpired(string idToken, TimeProvider timeProvider)
    {
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        return token.ValidTo <= timeProvider.GetUtcNow().UtcDateTime;
    }

    // Shared decision point: keep, refresh, or reject the current id token.
    public static async Task<RefreshOutcome> ValidateAsync(
        string? idToken,
        string? refreshToken,
        HttpClient httpClient,
        Uri? tokenEndpoint,
        string clientId,
        string? clientSecret,
        bool alwaysConfirmWithProvider,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (idToken is null)
        {
            return RefreshOutcome.NothingToValidate;
        }

        if (!alwaysConfirmWithProvider && !IsExpired(idToken, timeProvider))
        {
            return RefreshOutcome.StillValid;
        }

        if (refreshToken is null || tokenEndpoint is null)
        {
            return RefreshOutcome.Rejected;
        }

        RefreshedTokens? refreshed = await TryRefreshTokensAsync(
            httpClient, tokenEndpoint, clientId, clientSecret, refreshToken, cancellationToken);

        return refreshed is null ? RefreshOutcome.Rejected : RefreshOutcome.Refreshed(refreshed);
    }

    private static async Task<RefreshedTokens?> TryRefreshTokensAsync(
        HttpClient httpClient,
        Uri tokenEndpoint,
        string clientId,
        string? clientSecret,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using FormUrlEncodedContent content = new(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret ?? string.Empty,
                ["refresh_token"] = refreshToken,
            });
            using HttpResponseMessage response = await httpClient.PostAsync(tokenEndpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            TokenResponse? tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            // A refresh response without id_token is unusable.
            return tokenResponse?.IdToken is { } idToken
                ? new RefreshedTokens(idToken, tokenResponse.RefreshToken)
                : null;
        }
        catch (HttpRequestException)
        {
            // From the caller's perspective, an unreachable provider is equivalent to a
            // rejected refresh.
            return null;
        }
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        // Present when the identity provider rotates refresh tokens on every use
        // (Keycloak's default) - must be persisted, or the *next* refresh attempt
        // reuses an already-spent token and fails.
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }
    }
}

// A token still good as-is (StillValid) needs no caller action. NothingToValidate means
// there was no id_token to check at all - callers leave whatever they had alone, rather
// than guessing at intent. Refreshed carries the new tokens for the caller to persist
// and use. Rejected means neither the original id_token nor a refresh attempt produced
// anything usable - the caller's own concern (Program.cs) is what to do about that.
internal readonly record struct RefreshOutcome
{
    private RefreshOutcome(RefreshOutcomeKind kind, RefreshedTokens? tokens)
    {
        Kind = kind;
        Tokens = tokens;
    }

    public RefreshOutcomeKind Kind { get; }

    public RefreshedTokens? Tokens { get; }

    public static RefreshOutcome NothingToValidate { get; } = new(RefreshOutcomeKind.NothingToValidate, null);

    public static RefreshOutcome StillValid { get; } = new(RefreshOutcomeKind.StillValid, null);

    public static RefreshOutcome Rejected { get; } = new(RefreshOutcomeKind.Rejected, null);

    public static RefreshOutcome Refreshed(RefreshedTokens tokens)
    {
        return new RefreshOutcome(RefreshOutcomeKind.Refreshed, tokens);
    }
}

internal enum RefreshOutcomeKind
{
    NothingToValidate,
    StillValid,
    Refreshed,
    Rejected,
}

internal sealed record RefreshedTokens(string IdToken, string? RefreshToken);
