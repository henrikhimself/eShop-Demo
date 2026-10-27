// <copyright file="PrerequisiteCheckerTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Prerequisites;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Xunit;

namespace Hj.EShop.Cli.Tests.Prerequisites;

// This suite only runs on x86-64 dev/CI hosts (see README.md's Prerequisites section
// and PrerequisiteChecker's own architecture check) - RuntimeInformation.ProcessArchitecture
// is always X64 here, so "everything matches" below has no separate arm64 branch to cover.
public sealed class PrerequisiteCheckerTests
{
    [Fact]
    public async Task CheckAsync_EverythingMatchesPins_ReturnsNoIssues()
    {
        FakeProcessRunner processRunner = new(request => request.FileName switch
        {
            "dotnet" => new ProcessResult(0, "10.0.100\n", string.Empty),
            "node" => new ProcessResult(0, "v20.11.0\n", string.Empty),
            "pnpm" => new ProcessResult(0, "9.1.0\n", string.Empty),
            _ => new ProcessResult(1, string.Empty, "unexpected tool"),
        });
        PrerequisiteChecker checker = new(
            new FakeLocalToolLocator("dotnet", "node", "pnpm"),
            processRunner,
            new FakePinnedVersionReader("20.11.0", "9.1.0"),
            new RepoPaths("/repo"));

        PrerequisiteCheckResult result = await checker.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.GeneralIssues);
        Assert.Empty(result.LocalToolIssues);
        Assert.Empty(result.UnusableLocalTools);
    }

    [Fact]
    public async Task CheckAsync_DotnetNotOnPath_ReportsDotnetIssue()
    {
        PrerequisiteChecker checker = new(
            new FakeLocalToolLocator("node", "pnpm"),
            new FakeProcessRunner(_ => new ProcessResult(0, "v20.11.0\n", string.Empty)),
            new FakePinnedVersionReader("20.11.0", "9.1.0"),
            new RepoPaths("/repo"));

        PrerequisiteCheckResult result = await checker.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Contains(result.LocalToolIssues, issue => issue.StartsWith("dotnet:", StringComparison.Ordinal));
        Assert.Contains("dotnet", result.UnusableLocalTools);
    }

    [Fact]
    public async Task CheckAsync_NodeVersionMismatch_ReportsMismatchIssue()
    {
        FakeProcessRunner processRunner = new(request => request.FileName switch
        {
            "dotnet" => new ProcessResult(0, string.Empty, string.Empty),
            "node" => new ProcessResult(0, "v18.0.0\n", string.Empty),
            "pnpm" => new ProcessResult(0, "9.1.0\n", string.Empty),
            _ => new ProcessResult(1, string.Empty, "unexpected tool"),
        });
        PrerequisiteChecker checker = new(
            new FakeLocalToolLocator("dotnet", "node", "pnpm"),
            processRunner,
            new FakePinnedVersionReader("20.11.0", "9.1.0"),
            new RepoPaths("/repo"));

        PrerequisiteCheckResult result = await checker.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Contains("node: version mismatch - local 18.0.0, .nvmrc pins 20.11.0.", result.LocalToolIssues);
        Assert.Contains("node", result.UnusableLocalTools);
    }

    [Fact]
    public async Task CheckAsync_PnpmNotOnPath_ReportsInstallInstructions()
    {
        FakeProcessRunner processRunner = new(request => request.FileName switch
        {
            "dotnet" => new ProcessResult(0, string.Empty, string.Empty),
            "node" => new ProcessResult(0, "v20.11.0\n", string.Empty),
            _ => new ProcessResult(1, string.Empty, "unexpected tool"),
        });
        PrerequisiteChecker checker = new(
            new FakeLocalToolLocator("dotnet", "node"),
            processRunner,
            new FakePinnedVersionReader("20.11.0", "9.1.0"),
            new RepoPaths("/repo"));

        PrerequisiteCheckResult result = await checker.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Contains(result.LocalToolIssues, issue => issue.StartsWith("pnpm:", StringComparison.Ordinal));
        Assert.Contains("pnpm", result.UnusableLocalTools);
    }
}
