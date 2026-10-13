// <copyright file="OidcSignOutTokenRefreshTests.cs" company="Henrik Jensen">
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
using System.Net;
using System.Text;
using Xunit;

namespace Hj.EShop.TicketStore.Tests;

public sealed class OidcSignOutTokenRefreshTests
{
    private static readonly Uri _tokenEndpoint = new("https://keycloak.example/protocol/openid-connect/token");

    [Fact]
    public void IsExpired_ReturnsTrue_WhenValidToIsInThePast()
    {
        string token = CreateJwt(DateTime.UtcNow.AddMinutes(-5));

        Assert.True(OidcSignOutTokenRefresh.IsExpired(token, TimeProvider.System));
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenValidToIsInTheFuture()
    {
        string token = CreateJwt(DateTime.UtcNow.AddMinutes(5));

        Assert.False(OidcSignOutTokenRefresh.IsExpired(token, TimeProvider.System));
    }

    [Fact]
    public async Task ValidateAsync_ReturnsNothingToValidate_WhenThereIsNoIdToken()
    {
        using HttpClient client = new(new RecordingHttpMessageHandler(HttpStatusCode.OK, "{}"));

        RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
            idToken: null,
            refreshToken: "a-refresh-token",
            client,
            _tokenEndpoint,
            "seller-portal",
            "client-secret",
            alwaysConfirmWithProvider: false,
            TimeProvider.System,
            CancellationToken.None);

        Assert.Equal(RefreshOutcomeKind.NothingToValidate, outcome.Kind);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsStillValid_WhenTheIdTokenHasNotExpired()
    {
        using HttpClient client = new(new RecordingHttpMessageHandler(HttpStatusCode.OK, "{}"));
        string idToken = CreateJwt(DateTime.UtcNow.AddMinutes(5));

        RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
            idToken,
            refreshToken: "a-refresh-token",
            client,
            _tokenEndpoint,
            "seller-portal",
            "client-secret",
            alwaysConfirmWithProvider: false,
            TimeProvider.System,
            CancellationToken.None);

        Assert.Equal(RefreshOutcomeKind.StillValid, outcome.Kind);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsRefreshed_WhenTheTokenEndpointSucceeds()
    {
        using HttpClient client = new(new RecordingHttpMessageHandler(
            HttpStatusCode.OK, """{"id_token":"fresh-id-token","refresh_token":"rotated-refresh-token"}"""));
        string idToken = CreateJwt(DateTime.UtcNow.AddMinutes(-5));

        RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
            idToken,
            refreshToken: "a-refresh-token",
            client,
            _tokenEndpoint,
            "seller-portal",
            "client-secret",
            alwaysConfirmWithProvider: false,
            TimeProvider.System,
            CancellationToken.None);

        Assert.Equal(RefreshOutcomeKind.Refreshed, outcome.Kind);
        Assert.Equal("fresh-id-token", outcome.Tokens?.IdToken);
        Assert.Equal("rotated-refresh-token", outcome.Tokens?.RefreshToken);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsRejected_WhenThereIsNoRefreshTokenToTry()
    {
        using HttpClient client = new(new RecordingHttpMessageHandler(HttpStatusCode.OK, "{}"));
        string idToken = CreateJwt(DateTime.UtcNow.AddMinutes(-5));

        RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
            idToken,
            refreshToken: null,
            client,
            _tokenEndpoint,
            "seller-portal",
            "client-secret",
            alwaysConfirmWithProvider: false,
            TimeProvider.System,
            CancellationToken.None);

        Assert.Equal(RefreshOutcomeKind.Rejected, outcome.Kind);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsRejected_WhenTheTokenEndpointIsUnknown()
    {
        using HttpClient client = new(new RecordingHttpMessageHandler(HttpStatusCode.OK, "{}"));
        string idToken = CreateJwt(DateTime.UtcNow.AddMinutes(-5));

        RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
            idToken,
            refreshToken: "a-refresh-token",
            client,
            tokenEndpoint: null,
            "seller-portal",
            "client-secret",
            alwaysConfirmWithProvider: false,
            TimeProvider.System,
            CancellationToken.None);

        Assert.Equal(RefreshOutcomeKind.Rejected, outcome.Kind);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsRejected_WhenTheRefreshTokenIsRejected()
    {
        // The identity provider has no memory of this session at all, e.g. Keycloak
        // restarted and forgot it - the same outcome an expired id_token led to.
        using HttpClient client = new(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));
        string idToken = CreateJwt(DateTime.UtcNow.AddMinutes(-5));

        RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
            idToken,
            refreshToken: "a-refresh-token",
            client,
            _tokenEndpoint,
            "seller-portal",
            "client-secret",
            alwaysConfirmWithProvider: false,
            TimeProvider.System,
            CancellationToken.None);

        Assert.Equal(RefreshOutcomeKind.Rejected, outcome.Kind);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsRejected_WhenTheIdentityProviderIsUnreachable()
    {
        using HttpClient client = new(new ThrowingHttpMessageHandler());
        string idToken = CreateJwt(DateTime.UtcNow.AddMinutes(-5));

        RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
            idToken,
            refreshToken: "a-refresh-token",
            client,
            _tokenEndpoint,
            "seller-portal",
            "client-secret",
            alwaysConfirmWithProvider: false,
            TimeProvider.System,
            CancellationToken.None);

        Assert.Equal(RefreshOutcomeKind.Rejected, outcome.Kind);
    }

    [Fact]
    public async Task ValidateAsync_AlwaysConfirmWithProviderTrueAndTokenNotExpired_StillAttemptsRefresh()
    {
        // Proves the fix: alwaysConfirmWithProvider bypasses the exp-based shortcut, so
        // a token that looks fine locally still gets confirmed against the identity
        // provider - here, that confirmation succeeds.
        using HttpClient client = new(new RecordingHttpMessageHandler(
            HttpStatusCode.OK, """{"id_token":"fresh-id-token","refresh_token":"rotated-refresh-token"}"""));
        string idToken = CreateJwt(DateTime.UtcNow.AddMinutes(5));

        RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
            idToken,
            refreshToken: "a-refresh-token",
            client,
            _tokenEndpoint,
            "seller-portal",
            "client-secret",
            alwaysConfirmWithProvider: true,
            TimeProvider.System,
            CancellationToken.None);

        Assert.Equal(RefreshOutcomeKind.Refreshed, outcome.Kind);
    }

    [Fact]
    public async Task ValidateAsync_AlwaysConfirmWithProviderTrueAndRefreshRejected_ReturnsRejected()
    {
        // The exact bug this fixes: a token that isn't expired yet, but whose identity
        // provider has already forgotten it (e.g. a Keycloak restart) - only caught
        // because alwaysConfirmWithProvider forces the refresh attempt anyway.
        using HttpClient client = new(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));
        string idToken = CreateJwt(DateTime.UtcNow.AddMinutes(5));

        RefreshOutcome outcome = await OidcSignOutTokenRefresh.ValidateAsync(
            idToken,
            refreshToken: "a-refresh-token",
            client,
            _tokenEndpoint,
            "seller-portal",
            "client-secret",
            alwaysConfirmWithProvider: true,
            TimeProvider.System,
            CancellationToken.None);

        Assert.Equal(RefreshOutcomeKind.Rejected, outcome.Kind);
    }

    private static string CreateJwt(DateTime validTo)
    {
        // Unsigned on purpose: IsExpired only reads the exp claim, it never validates a
        // signature - the token already came from the caller's own encrypted,
        // persisted ticket, not from an untrusted caller.
        JwtSecurityToken token = new(expires: validTo);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string jsonContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(statusCode)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection refused.");
        }
    }
}
