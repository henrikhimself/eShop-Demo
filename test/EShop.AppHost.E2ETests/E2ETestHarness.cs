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
        // DistributedApplicationTestingBuilder randomizes every resource's port by
        // default (DcpOptions.RandomizePorts) - harmless for most resources here, but
        // the reverse proxy's fixed 8443 is baked into the static Keycloak realm
        // client's redirect URIs (eshop-realm.json) and the Bff's/Storefront's OIDC
        // authority (PLAN-2.md §4), so it must keep the same port under test that it
        // uses under a real `aspire run`/`eshop run`.
        Environment.SetEnvironmentVariable("DcpPublisher__RandomizePorts", "false");
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

    private static void RecordConsoleError(List<string> events, IConsoleMessage message)
    {
        if (message.Type == "error")
        {
            events.Add($"console error: {SanitizeBrowserDiagnostic(message.Text)}");
        }
    }

    private static void RecordRequest(List<string> events, string eventName, IRequest request)
    {
        Uri uri = new(request.Url);
        if (uri.AbsolutePath.StartsWith("/bff/", StringComparison.Ordinal))
        {
            events.Add($"{eventName}: {request.Method} {uri.AbsolutePath}");
        }
    }

    private static string SanitizeBrowserDiagnostic(string message)
    {
        return message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || message.Contains("password", StringComparison.OrdinalIgnoreCase)
            ? "[redacted]"
            : message;
    }

    private static void RecordResponse(List<string> events, IResponse response)
    {
        Uri uri = new(response.Url);
        if (uri.AbsolutePath.StartsWith("/bff/", StringComparison.Ordinal))
        {
            events.Add($"response: {response.Status} {response.Request.Method} {uri.AbsolutePath}");
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

    public static async Task LogInAsync(IPage page, Uri webBaseAddress, string loginPath, string username, string password)
    {
        await page.GotoAsync(new Uri(webBaseAddress, loginPath).ToString());

        await Expect(page.Locator("#username")).ToBeVisibleAsync();

        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(password);
        await page.Locator("#kc-login").ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex($"^{Regex.Escape(webBaseAddress.ToString())}"));
    }

    // For a browser that already holds a Keycloak SSO session cookie (from an earlier
    // LogInAsync at a different application, same identity.eshop.local host) - Keycloak
    // approves the new client's authorization request silently, with no login form to
    // fill in.
    public static async Task SsoLoginAsync(IPage page, Uri webBaseAddress, string loginPath)
    {
        await page.GotoAsync(new Uri(webBaseAddress, loginPath).ToString());

        await Expect(page).ToHaveURLAsync(new Regex($"^{Regex.Escape(webBaseAddress.ToString())}"));
    }

    public static async Task LogOutAsync(IPage page, Uri webBaseAddress)
    {
        try
        {
            await page.GotoAsync(new Uri(webBaseAddress, "bff/logout").ToString());
        }
        catch (PlaywrightException exception) when (exception.Message.Contains("net::ERR_ABORTED", StringComparison.Ordinal))
        {
            // The OIDC redirect chain can replace the direct navigation before
            // Playwright observes its load event. The final browser origin remains the
            // behavior this test must prove.
        }

        try
        {
            await Expect(page).ToHaveURLAsync(new Regex($"^{Regex.Escape(webBaseAddress.ToString())}"));
        }
        catch (PlaywrightException)
        {
            Uri finalUri = new(page.Url);
            Console.WriteLine($"Logout did not return to the Seller Portal Web origin. Final location: {finalUri.GetLeftPart(UriPartial.Path)}");
            throw;
        }
    }

    public static Task WaitForDraftsPageReadyAsync(IPage page)
    {
        return Expect(page.GetByTestId("draft-list")).ToHaveAttributeAsync(
            "data-loaded",
            "true",
            new LocatorAssertionsToHaveAttributeOptions { Timeout = 30_000 });
    }

    public static async Task<IResponse> ClickAndWaitForDraftCreationResponseAsync(
        IPage page,
        string expectedPath,
        Func<Task> action)
    {
        List<string> events = [];
        EventHandler<IRequest> requestHandler = (_, request) => RecordRequest(events, "request", request);
        EventHandler<IResponse> responseHandler = (_, response) => RecordResponse(events, response);
        EventHandler<IRequest> requestFailedHandler = (_, request) => RecordRequest(events, $"failed ({request.Failure})", request);
        EventHandler<IConsoleMessage> consoleHandler = (_, message) => RecordConsoleError(events, message);

        page.Request += requestHandler;
        page.Response += responseHandler;
        page.RequestFailed += requestFailedHandler;
        page.Console += consoleHandler;
        try
        {
            return await page.RunAndWaitForResponseAsync(
                async () =>
                {
                    events.Add("action: click started");
                    await action();
                    events.Add("action: click completed");
                },
                response => response.Request.Method == "POST"
                    && new Uri(response.Url).AbsolutePath == expectedPath,
                new PageRunAndWaitForResponseOptions { Timeout = 30_000 });
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"Timed out waiting for POST {expectedPath}. Browser BFF request sequence:\n{string.Join(Environment.NewLine, events)}");
            throw;
        }
        finally
        {
            page.Request -= requestHandler;
            page.Response -= responseHandler;
            page.RequestFailed -= requestFailedHandler;
            page.Console -= consoleHandler;
        }
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

internal sealed class E2ETestSession(DistributedApplication app, IPlaywright playwright, IBrowser browser) : IAsyncDisposable
{
    public IBrowser Browser => browser;

    public Uri GetBaseAddress(string resourceName, string? endpointName = null)
    {
        // Seller Portal Web/Storefront are no longer reachable through a direct
        // external Aspire endpoint in local development (PLAN-2.md §3) - their OIDC
        // clients are also only registered for the reverse-proxy host (§4.1), so
        // Playwright must start there instead of the internal endpoint.
        if (endpointName is null && KnownValues.ReverseProxyHostNames.TryGetValue(resourceName, out string? hostName))
        {
            return new Uri($"https://{hostName}:{KnownNames.ReverseProxyHttpsPort}/");
        }

        using HttpClient client = app.CreateHttpClient(resourceName, endpointName);
        Uri? baseAddress = client.BaseAddress;
        Assert.NotNull(baseAddress);
        return baseAddress;
    }

    // Simulates the narrow remaining trigger for the stale-id_token bug
    // (doc/CHRONICLE.md): Keycloak's AppHost resource is ContainerLifetime.Persistent
    // (PLAN-2.md §6.2 item 7), so an ordinary restart alone no longer rotates its
    // signing keys - this method force-deletes Keycloak's own embedded database first
    // to still reach the case a real, rarer full data loss (a lost/recreated container)
    // would cause, while the Bff's own (persistent) auth ticket - including the id_token
    // saved at login - is untouched. The "seller-portal" client itself survives, since
    // it comes back through the static realm import (PLAN-2.md §4.1) on every Keycloak
    // boot.
    public async Task RestartKeycloakAsync(CancellationToken cancellationToken)
    {
        string containerId = (await RunDockerAsync(
            ["ps", "--filter", $"name={KnownNames.ResourceKeycloak}-", "--format", "{{.ID}}"], cancellationToken)).Trim();
        Assert.False(string.IsNullOrEmpty(containerId), "Could not find a running Keycloak container to restart.");

        // `docker restart` alone reuses the container's existing writable filesystem,
        // including Keycloak's own embedded H2 database
        // (/opt/keycloak/data/h2/keycloakdb.mv.db) - the realm's signing keys and
        // sessions would all survive a bare restart, unlike the real "no persistent
        // volume" restart this is meant to simulate (a full container recreation with
        // no filesystem carried over). Deleting it first forces Keycloak to reimport
        // the realm and mint fresh keys on the next boot, while keeping the same
        // container/port so no re-import race is needed.
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
