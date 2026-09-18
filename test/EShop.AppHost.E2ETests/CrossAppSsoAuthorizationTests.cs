// <copyright file="CrossAppSsoAuthorizationTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;

namespace Hj.EShop.AppHost.E2ETests;

// Starts the real AppHost and drives headless Chromium against it, same shape as
// SellerPortalLoginTests - see that file's own remarks. Proves PLAN-2.md §6.2 item 5:
// the reverse proxy's single identity.eshop.local host means Keycloak's own SSO session
// cookie is shared across every application, so signing into one silently authenticates
// the same browser at another - the Bff's own "SellerOnly" policy, not the login flow,
// must be what keeps a non-Seller identity out of Seller-only data.
public sealed class CrossAppSsoAuthorizationTests
{
    [Fact]
    public async Task StorefrontAdminSsoAtSellerPortal_StillDeniedSellerOnlyData()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using E2ETestSession session = await E2ETestHarness.StartAsync(
            [KnownNames.ResourceSellerPortalBff, KnownNames.ResourceSellerPortalWeb, KnownNames.ResourceStorefrontWeb, KnownNames.ResourceDevReverseProxy],
            cancellationToken);

        Uri sellerPortalBaseAddress = session.GetBaseAddress(KnownNames.ResourceSellerPortalWeb);
        Uri storefrontBaseAddress = session.GetBaseAddress(KnownNames.ResourceStorefrontWeb);

        // One browser context for both logins - the point of this test is that
        // Keycloak's own SSO session cookie (identity.eshop.local, common to every
        // client through the reverse proxy) carries over between them.
        IPage page = await session.NewPageAsync();

        await E2ETestHarness.LogInAsync(
            page,
            storefrontBaseAddress,
            TestCredentials.StorefrontAdmin.LoginPath,
            TestCredentials.StorefrontAdmin.Username,
            TestCredentials.StorefrontAdmin.Password);

        // No credentials to enter here - Keycloak approves the Seller Portal client's
        // authorization request silently using the SSO session Storefront's login just
        // established, proving both applications really do share one identity host.
        await E2ETestHarness.SsoLoginAsync(page, sellerPortalBaseAddress, TestCredentials.SellerPortalSeller.LoginPath);

        IAPIResponse draftsResponse = await page.APIRequest.GetAsync(
            new Uri(sellerPortalBaseAddress, "bff/api/drafts").ToString(),
            new APIRequestContextOptions { Timeout = (float)E2ETestHarness.DefaultTimeout.TotalMilliseconds });

        // "admin" only holds the SiteAdministrator realm role - authenticated, but not
        // a Seller, so the Bff's "SellerOnly" policy must forbid this request outright.
        Assert.Equal(403, draftsResponse.Status);
    }
}
