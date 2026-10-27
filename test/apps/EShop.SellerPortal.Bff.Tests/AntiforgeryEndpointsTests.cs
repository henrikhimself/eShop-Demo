// <copyright file="AntiforgeryEndpointsTests.cs" company="Henrik Jensen">
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

using System.Net;
using Hj.EShop.SellerPortal.Bff.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// Doubles as regression coverage that CSRF protection is actually enforced, not just
// configured - see AntiforgeryEndpointFilter.
public sealed class AntiforgeryEndpointsTests : IAsyncLifetime
{
    private readonly SellerPortalWebApplicationFactory factory = new();

    public async ValueTask InitializeAsync()
    {
        await factory.EnsureDatabaseCreatedAsync();
    }

    public ValueTask DisposeAsync()
    {
        factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetToken_Unauthenticated_Returns401()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/antiforgery/token", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetToken_AuthenticatedSeller_SetsXsrfTokenCookie()
    {
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, "seller-antiforgery-1");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/antiforgery/token", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PostMovieDraft_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        // 400, not 403: a stale/missing antiforgery pairing must stay distinguishable
        // from a genuine "authenticated but not a Seller" access-denied response, since
        // lib/bff-fetch.ts reacts to only one of the two (see AntiforgeryEndpointFilter).
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, "seller-antiforgery-2");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");

        HttpResponseMessage response = await client.PostAsync(
            new Uri("/bff/api/drafts/movies", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.Contains(AntiforgeryEndpointFilter.InvalidHeaderName));
    }

    [Fact]
    public async Task PostMovieDraft_WithValidAntiforgeryToken_Succeeds()
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, "seller-antiforgery-3");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PostAsync(
            new Uri("/bff/api/drafts/movies", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
