using System.Diagnostics;
using System.Text.RegularExpressions;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Hj.EShop.Common;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Hj.EShop.AppHost.E2ETests;

// Shared bootstrap for every browser E2E test class. Each [Fact] still starts and
// disposes its own full AppHost/browser (see AssemblyInfo.cs's
// DisableTestParallelization - two concurrent stacks starve each other's Docker
// containers) - only the code that does so is shared here, not the lifetime.
internal static class E2ETestHarness
{
    // Generous: SQL Server briefly rejects the "sa" login during first-time bootstrap,
    // a cold run also pulls several container images for the first time, and some
    // tests additionally wait on an async Service Bus round trip.
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(300);

    public static async Task<E2ETestSession> StartAsync(IReadOnlyList<string> resourcesToWaitFor, CancellationToken cancellationToken)
    {
        IDistributedApplicationTestingBuilder appHostBuilder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(cancellationToken: cancellationToken);

        DistributedApplication app = await appHostBuilder.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        try
        {
            await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

            foreach (string resourceName in resourcesToWaitFor)
            {
                await app.ResourceNotifications.WaitForResourceHealthyAsync(resourceName, cancellationToken)
                    .WaitAsync(DefaultTimeout, cancellationToken);
            }

            IPlaywright playwright = await Playwright.CreateAsync();
            try
            {
                IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                return new E2ETestSession(app, playwright, browser);
            }
            catch
            {
                playwright.Dispose();
                throw;
            }
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public static void ApplyDefaultTimeouts(IPage page)
    {
        // Playwright's own default timeouts (30s action, 5s assertion) are too tight
        // for a cold container's first request through a resource - raised to
        // DefaultTimeout.
        page.SetDefaultTimeout((float)DefaultTimeout.TotalMilliseconds);
        SetDefaultExpectTimeout((float)DefaultTimeout.TotalMilliseconds);
    }

    // Single source of truth for the seeded-Seller login steps.
    public static async Task LogInAsTestSellerAsync(IPage page, Uri webBaseAddress)
    {
        await page.GotoAsync(webBaseAddress.ToString());
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Log in" }).ClickAsync();

        // The eshop realm uses Keycloak's default login theme (no custom branding, see
        // Realms/eshop-realm.json), so its standard username field is a reliable,
        // theme-default thing to assert the redirect chain actually landed on.
        await Expect(page.Locator("#username")).ToBeVisibleAsync();

        // "test-seller" is seeded in Realms/eshop-realm.json with the "Seller" realm
        // role already assigned.
        await page.Locator("#username").FillAsync("test-seller");
        await page.Locator("#password").FillAsync("TestSeller123!");
        await page.Locator("#kc-login").ClickAsync();

        // Confirms the OIDC callback completed and landed back on the Web origin, not
        // still on Keycloak (e.g. after a rejected login).
        await Expect(page).ToHaveURLAsync(new Regex($"^{Regex.Escape(webBaseAddress.ToString())}"));
    }

    // Generic version of LogInAsTestSellerAsync above, for apps with no landing-page
    // "Log in" link to click (e.g. EShop.StoreFront.Web, which has no shell pages yet -
    // STOREFRONT-PLAN.md) - navigates straight to the app's own login-challenge route.
    public static async Task LogInAsync(IPage page, Uri webBaseAddress, string loginPath, string username, string password)
    {
        await page.GotoAsync(new Uri(webBaseAddress, loginPath).ToString());

        // Same reasoning as LogInAsTestSellerAsync - the eshop realm uses Keycloak's
        // default login theme, so its standard username field is a reliable,
        // theme-default thing to assert the redirect chain actually landed on.
        await Expect(page.Locator("#username")).ToBeVisibleAsync();

        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(password);
        await page.Locator("#kc-login").ClickAsync();

        // Confirms the OIDC callback completed and landed back on the app's own origin,
        // not still on Keycloak (e.g. after a rejected login).
        await Expect(page).ToHaveURLAsync(new Regex($"^{Regex.Escape(webBaseAddress.ToString())}"));
    }

    public static async Task WaitUntilAsync(Func<Task<bool>> probe, CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + DefaultTimeout;
        while (!await probe())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Timed out waiting for the expected condition.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}

// One AppHost + one Playwright browser per [Fact]. Dispose order mirrors the original
// per-test using/await-using stacking: browser, then the Playwright driver, then the
// AppHost.
internal sealed class E2ETestSession(DistributedApplication app, IPlaywright playwright, IBrowser browser) : IAsyncDisposable
{
    public IBrowser Browser => browser;

    public Uri GetBaseAddress(string resourceName, string? endpointName = null)
    {
        using HttpClient client = app.CreateHttpClient(resourceName, endpointName);
        Uri? baseAddress = client.BaseAddress;
        Assert.NotNull(baseAddress);
        return baseAddress;
    }

    // Simulates the real-world trigger for the stale-id_token bug (doc/CHRONICLE.md):
    // Keycloak has no persistent volume, so a restart rotates its signing keys and
    // drops the dynamically-provisioned "seller-portal" client, while the Bff's own
    // (persistent) auth ticket - including the id_token saved at login - is untouched.
    public async Task RestartKeycloakAsync(CancellationToken cancellationToken)
    {
        string containerId = (await RunDockerAsync(
            ["ps", "--filter", $"name={KnownNames.ResourceKeycloak}-", "--format", "{{.ID}}"], cancellationToken)).Trim();
        Assert.False(string.IsNullOrEmpty(containerId), "Could not find a running Keycloak container to restart.");

        // `docker restart` alone reuses the container's existing writable filesystem,
        // including Keycloak's own embedded H2 database
        // (/opt/keycloak/data/h2/keycloakdb.mv.db) - the realm's signing keys, sessions,
        // and the dynamically-provisioned client would all survive a bare restart,
        // unlike the real "no persistent volume" restart this is meant to simulate (a
        // full container recreation with no filesystem carried over). Deleting it first
        // forces Keycloak to reimport the realm and mint fresh keys on the next boot,
        // while keeping the same container/port so no re-provisioning race is needed.
        await RunDockerAsync(["exec", containerId, "rm", "-rf", "/opt/keycloak/data/h2"], cancellationToken);
        await RunDockerAsync(["restart", containerId], cancellationToken);

        // Polls Keycloak's own discovery endpoint rather than
        // ResourceNotifications.WaitForResourceHealthyAsync: right after `docker
        // restart`, Aspire hasn't yet observed the container going down, so a
        // health-state wait could return based on the stale "already healthy" snapshot
        // from before the restart instead of actually waiting for the new instance.
        // The endpoint is named "http" even though Aspire upgrades its scheme to
        // https for local dev certs - same endpoint AppHost.cs resolves via the
        // "services:keycloak:https:0" service-discovery key.
        Uri discoveryUri = new(
            GetBaseAddress(KnownNames.ResourceKeycloak, "http"), $"realms/{KnownNames.KeycloakRealmEShop}/.well-known/openid-configuration");
        using HttpClientHandler handler = new()
        {
            // Same reasoning as NewPageAsync's IgnoreHTTPSErrors below - Keycloak's
            // HTTPS endpoint uses the local dev cert, which isn't trusted here either.
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        using HttpClient client = new(handler);
        await E2ETestHarness.WaitUntilAsync(
            async () =>
            {
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(discoveryUri, cancellationToken);
                    return response.IsSuccessStatusCode;
                }
                catch (HttpRequestException)
                {
                    return false;
                }
            },
            cancellationToken);
    }

    private static async Task<string> RunDockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start docker.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        Assert.True(process.ExitCode == 0, $"docker {string.Join(' ', arguments)} failed: {await error}");
        return await output;
    }

    public async Task<IPage> NewPageAsync()
    {
        // Bypasses cert validation: Keycloak's HTTPS endpoint uses the local dev cert,
        // which headless Chromium doesn't trust automatically on Linux. Always
        // local/dev-only, never a real deployment - safe to bypass here.
        IPage page = await browser.NewPageAsync(new BrowserNewPageOptions { IgnoreHTTPSErrors = true });
        E2ETestHarness.ApplyDefaultTimeouts(page);
        return page;
    }

    public async ValueTask DisposeAsync()
    {
        await browser.DisposeAsync();
        playwright.Dispose();
        await app.DisposeAsync();
    }
}
