// <copyright file="AppHostGuardIntegrationTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Xunit;

namespace Hj.EShop.Cli.Tests.Integration;

// Needs a real local `aspire` CLI on PATH - exercises the actual `aspire ps --format
// Json` output shape, not a hand-written JSON fixture.
[Trait("Category", "Integration")]
public sealed class AppHostGuardIntegrationTests
{
    [Fact]
    public async Task IsAlreadyRunningAsync_NoSessionRunningForThisRepo_ReturnsFalse()
    {
        RepoPaths paths = new(new RepoRootLocator().Find());
        GlobalOptionsAccessor accessor = new();
        accessor.Resolve(new GlobalOptions(ExecutionMode.Auto, OutputMode.Human));

        // Auto + a real LocalToolLocator resolves `aspire` locally as long as it's on
        // PATH, so the never-invoked FakeContainerRunner here is just a safe stand-in.
        ToolExecutor toolExecutor = new(
            accessor, new LocalToolLocator(), new FakeContainerRunner(), new ProcessRunner(), new RecordingOutputSink(), paths);
        AppHostGuard guard = new(toolExecutor, paths);

        bool alreadyRunning = await guard.IsAlreadyRunningAsync(TestContext.Current.CancellationToken);

        Assert.False(alreadyRunning);
    }
}
