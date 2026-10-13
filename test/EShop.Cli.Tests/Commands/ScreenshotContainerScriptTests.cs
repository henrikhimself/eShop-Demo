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
    public void Script_WaitsForTheFullLoginRedirectChainToReturnToTheResourceOrigin_NotJustDomContentLoadedAfterTheClick()
    {
        // A resource can start OIDC from either a dedicated login endpoint or a protected
        // route. Waiting for the target origin is the resource-agnostic point at which its
        // OIDC callback has finished setting the authentication cookie.
        Assert.Contains("await page.waitForURL((url) => url.origin === origin", ScreenshotContainerScript.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("/bff/signin-oidc", ScreenshotContainerScript.Script, StringComparison.Ordinal);
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
