// <copyright file="AppHostGuardTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.AppHost;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Xunit;

namespace Hj.EShop.Cli.Tests.AppHost;

public sealed class AppHostGuardTests
{
    [Fact]
    public async Task IsAlreadyRunningAsync_MatchingRunningSession_ReturnsTrue()
    {
        RepoPaths paths = new("/repo");
        string json = "[{\"appHostPath\":\"" + paths.AppHostProject + "\",\"status\":\"running\"}]";
        FakeToolExecutor toolExecutor = new(_ => new ProcessResult(0, json, string.Empty));
        AppHostGuard guard = new(toolExecutor, paths);

        Assert.True(await guard.IsAlreadyRunningAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsAlreadyRunningAsync_MatchingButStoppedSession_ReturnsFalse()
    {
        RepoPaths paths = new("/repo");
        string json = "[{\"appHostPath\":\"" + paths.AppHostProject + "\",\"status\":\"stopped\"}]";
        FakeToolExecutor toolExecutor = new(_ => new ProcessResult(0, json, string.Empty));
        AppHostGuard guard = new(toolExecutor, paths);

        Assert.False(await guard.IsAlreadyRunningAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsAlreadyRunningAsync_NoSessions_ReturnsFalse()
    {
        FakeToolExecutor toolExecutor = new(_ => new ProcessResult(0, "[]", string.Empty));
        AppHostGuard guard = new(toolExecutor, new RepoPaths("/repo"));

        Assert.False(await guard.IsAlreadyRunningAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsAlreadyRunningAsync_AspirePsFails_ReturnsFalse()
    {
        FakeToolExecutor toolExecutor = new(_ => new ProcessResult(1, string.Empty, "aspire: command failed"));
        AppHostGuard guard = new(toolExecutor, new RepoPaths("/repo"));

        Assert.False(await guard.IsAlreadyRunningAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsAlreadyRunningAsync_AspireReportsContainerMountPath_StillMatches()
    {
        // aspire ps run inside the utility container reports the repo mounted at
        // /workspace, not the host's own absolute repo root - the guard must still
        // recognize this as the same AppHost project.
        RepoPaths paths = new("/repo");
        const string containerReportedPath = "/workspace/src/EShop.AppHost/EShop.AppHost.csproj";
        string json = "[{\"appHostPath\":\"" + containerReportedPath + "\",\"status\":\"running\"}]";
        FakeToolExecutor toolExecutor = new(_ => new ProcessResult(0, json, string.Empty));
        AppHostGuard guard = new(toolExecutor, paths);

        Assert.True(await guard.IsAlreadyRunningAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsAlreadyRunningAsync_UnrelatedProjectPath_ReturnsFalse()
    {
        RepoPaths paths = new("/repo");
        string json = "[{\"appHostPath\":\"/workspace/src/apps/EShop.SellerPortal.Bff/EShop.SellerPortal.Bff.csproj\",\"status\":\"running\"}]";
        FakeToolExecutor toolExecutor = new(_ => new ProcessResult(0, json, string.Empty));
        AppHostGuard guard = new(toolExecutor, paths);

        Assert.False(await guard.IsAlreadyRunningAsync(TestContext.Current.CancellationToken));
    }
}
