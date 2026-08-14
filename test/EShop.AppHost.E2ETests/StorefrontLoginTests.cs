using System.Text.Json;
using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;

namespace Hj.EShop.AppHost.E2ETests;

// Starts the real AppHost and drives headless Chromium against it, same shape as
// SellerPortalLoginTests - see that file's own remarks. Exercises
// STOREFRONT-PLAN.md's Phase 1: dual-provider OIDC wiring, the CMS user/role sync gap
// fix (StorefrontAuthConfiguration's ISynchronizingUserService call), and the Keycloak
// role-claim mapping KeycloakStorefrontClientProvisioner sets up.
public sealed class StorefrontLoginTests
{
    [Fact]
    public async Task LoggingInAsAStorefrontStaffActorCarriesTheirRealmRoleClaim()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using E2ETestSession session = await E2ETestHarness.StartAsync(
            [KnownNames.ResourceStorefrontWeb], cancellationToken);

        Uri webBaseAddress = session.GetBaseAddress(KnownNames.ResourceStorefrontWeb);
        IPage page = await session.NewPageAsync();

        // "test-content-editor" is seeded in Realms/eshop-realm.json and assigned the
        // "ContentEditor" realm role by KeycloakStorefrontClientProvisioner.
        await E2ETestHarness.LogInAsync(page, webBaseAddress, "login", "test-content-editor", "TestContentEditor123!");

        // Confirms the "storefront-role" client scope actually surfaces the realm role
        // as a "role" claim StorefrontAuthConfiguration's ClaimActions.MapJsonKey turns
        // into a standard ClaimTypes.Role claim - not just that the login round trip
        // completed.
        IAPIResponse userResponse = await page.APIRequest.GetAsync(
            new Uri(webBaseAddress, "user").ToString(),
            new APIRequestContextOptions { Timeout = (float)E2ETestHarness.DefaultTimeout.TotalMilliseconds });
        Assert.True(userResponse.Ok, $"Expected a successful response, got {userResponse.Status}.");

        JsonElement[] claims = await userResponse.JsonAsync() is { } json
            ? [.. json.EnumerateArray()]
            : [];
        Assert.Contains(
            claims,
            claim => claim.GetProperty("type").GetString() == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
                && claim.GetProperty("value").GetString() == "ContentEditor");

        // Logout should succeed and land back on the Storefront's own origin.
        await page.GotoAsync(new Uri(webBaseAddress, "logout").ToString());

        IAPIResponse userAfterLogoutResponse = await page.APIRequest.GetAsync(
            new Uri(webBaseAddress, "user").ToString(),
            new APIRequestContextOptions { Timeout = (float)E2ETestHarness.DefaultTimeout.TotalMilliseconds });
        Assert.False(userAfterLogoutResponse.Ok, "Expected /user to require authentication again after logout.");
    }
}
