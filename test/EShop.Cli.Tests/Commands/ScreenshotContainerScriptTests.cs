// <copyright file="ScreenshotContainerScriptTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Commands;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands;

public sealed class ScreenshotContainerScriptTests
{
    [Fact]
    public void Script_UsesTheNodePlaywrightDriver_NotTheRawChromeOneLiner()
    {
        Assert.Contains("require('playwright')", ScreenshotContainerScript.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("--headless", ScreenshotContainerScript.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_ReusesSavedStorageStateAcrossInvocations_SoALoginSurvives()
    {
        Assert.Contains("storageState:", ScreenshotContainerScript.Script, StringComparison.Ordinal);
        Assert.Contains("context.storageState({ path: statePath })", ScreenshotContainerScript.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_OnlyDrivesTheLoginFormWhenLoginArgIsPassed()
    {
        Assert.Contains("if (args.login)", ScreenshotContainerScript.Script, StringComparison.Ordinal);
        Assert.Contains("#username", ScreenshotContainerScript.Script, StringComparison.Ordinal);
        Assert.Contains("#password", ScreenshotContainerScript.Script, StringComparison.Ordinal);
        Assert.Contains("#kc-login", ScreenshotContainerScript.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_WaitsForTheFullLoginRedirectChainToLandOnANonBffPage_NotJustDomContentLoadedAfterTheClick()
    {
        // Clicking the Keycloak login button only starts the redirect chain
        // (Keycloak -> /bff/signin-oidc -> the RedirectUri). The script waits for a URL
        // back on our origin and off /bff/* so the BFF has set the auth cookie before
        // the final `page.goto(args.url)`.
        Assert.Contains("await page.waitForURL(", ScreenshotContainerScript.Script, StringComparison.Ordinal);
        Assert.Contains("!url.pathname.startsWith('/bff/')", ScreenshotContainerScript.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_WaitsForNetworkIdleWithAHardWallClockCap()
    {
        Assert.Contains("waitForLoadState('networkidle')", ScreenshotContainerScript.Script, StringComparison.Ordinal);
        Assert.Contains("Promise.race", ScreenshotContainerScript.Script, StringComparison.Ordinal);
        Assert.Contains("setTimeout(resolve, 8000)", ScreenshotContainerScript.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void FileName_UsesCjsExtension_SoRequireResolvesTheGloballyInstalledPackage()
    {
        Assert.EndsWith(".cjs", ScreenshotContainerScript.FileName, StringComparison.Ordinal);
    }
}
