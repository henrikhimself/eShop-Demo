using System.Text.RegularExpressions;
using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Hj.EShop.AppHost.E2ETests;

// Reproduces the bug in doc/CHRONICLE.md's "Follow-up: a kept id_token can still go
// stale enough for Keycloak to reject it as id_token_hint" entry: OidcSignOutTokenRefresh
// only checks the saved id_token's own exp claim. A Keycloak restart rotates its signing
// keys and drops its dynamically-provisioned client without advancing that claim, so a
// still-not-expired id_token from before the restart passes the check unchanged and gets
// sent as id_token_hint to the new Keycloak instance, which rejects it -
// AuthConfiguration.cs's OnValidatePrincipal never even calls Keycloak in this case,
// since the exp check alone reports "still valid".
public sealed class SellerPortalStaleIdTokenLogoutTests
{
    [Fact]
    public async Task LoggingOutAfterAKeycloakRestart_StillReachesTheWebOrigin()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using E2ETestSession session = await E2ETestHarness.StartAsync(
            [KnownNames.ResourceSellerPortalBff, KnownNames.ResourceSellerPortalWeb], cancellationToken);

        Uri webBaseAddress = session.GetBaseAddress(KnownNames.ResourceSellerPortalWeb);
        IPage page = await session.NewPageAsync();
        await E2ETestHarness.LogInAsTestSellerAsync(page, webBaseAddress);

        // The id_token saved by this login is still comfortably within its own exp
        // window (Keycloak's default lifespan is several minutes; restarting an
        // already-pulled container takes a fraction of that) - it's stale only because
        // the identity provider that minted it is gone, not because time ran out.
        await session.RestartKeycloakAsync(cancellationToken);

        await page.GotoAsync(new Uri(webBaseAddress, "bff/logout").ToString());

        // Expected: the round trip through Keycloak's logout endpoint completes and
        // lands back on the Web origin, same as the happy-path assertion in
        // SellerPortalLoginTests. Today this fails - the browser dead-ends on
        // Keycloak's "Invalid parameter: id_token_hint" error page instead, because
        // OidcSignOutTokenRefresh.ValidateAsync never contacted Keycloak: the id_token's
        // own exp claim still says "not expired".
        await Expect(page).ToHaveURLAsync(new Regex($"^{Regex.Escape(webBaseAddress.ToString())}"));
    }
}
