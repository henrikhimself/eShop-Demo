// <copyright file="ContainerRunnerIntegrationTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Xunit;

namespace Hj.EShop.Cli.Tests.Integration;

// Needs real Docker and the eshop-utility:local image (see scripts/Containerfile) -
// excluded from the default `eshop test`/`dotnet test` run via
// --filter-not-trait "Category=Integration" (see UnitTestCommand).
[Trait("Category", "Integration")]
public sealed class ContainerRunnerIntegrationTests
{
    [Fact]
    public async Task RunAsync_RealDockerAndUtilityImage_RunsToolInsideContainer()
    {
        RepoPaths paths = new(new RepoRootLocator().Find());
        ProcessRunner processRunner = new();
        UtilityImageProvisioner imageProvisioner = new(processRunner, paths, () => new RecordingOutputSink(), new UtilityImageSourceHasher(paths));
        LinuxIdentityProvider identityProvider = new(processRunner);
        ContainerRunner containerRunner = new(processRunner, imageProvisioner, identityProvider, paths);

        ProcessResult result = await containerRunner.RunAsync(
            new ToolInvocation("uname", ["-m"]), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.StandardError);
        Assert.NotEmpty(result.StandardOutput.Trim());
    }
}
