// <copyright file="UnitTestCommandTests.cs" company="Henrik Jensen">
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

using System.Diagnostics;
using Hj.EShop.Cli.Commands.Test;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands.Test;

public sealed class UnitTestCommandTests
{
    [Fact]
    public async Task ExecuteAsync_BothTestRunsSucceed_ReturnsZero()
    {
        FakeToolExecutor toolExecutor = new();
        UnitTestCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        int exitCode = await ((ICommand<TestSettings>)command).ExecuteAsync(
            context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(toolExecutor.Invocations, i => i.Tool == "dotnet" && i.Arguments.Contains("test"));
        Assert.Contains(toolExecutor.Invocations, i => i.Tool == "pnpm" && i.Arguments.Contains("test"));
    }

    [Fact]
    public async Task ExecuteAsync_ExcludesIntegrationTraitFromTheDefaultRun()
    {
        FakeToolExecutor toolExecutor = new();
        UnitTestCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        await ((ICommand<TestSettings>)command).ExecuteAsync(context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        Assert.Contains(
            toolExecutor.Invocations,
            i => i.Tool == "dotnet" && i.Arguments.Contains("--filter-not-trait") && i.Arguments.Contains("Category=Integration"));
    }

    [Fact]
    public async Task ExecuteAsync_DotnetTestFails_ReturnsNonZero()
    {
        FakeToolExecutor toolExecutor = new(invocation => invocation.Tool == "dotnet" && invocation.Arguments.Contains("test")
            ? new ProcessResult(1, string.Empty, "failed")
            : new ProcessResult(0, string.Empty, string.Empty));
        UnitTestCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        int exitCode = await ((ICommand<TestSettings>)command).ExecuteAsync(
            context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_FrontendTestFails_ReturnsNonZero()
    {
        FakeToolExecutor toolExecutor = new(invocation => invocation.Tool == "pnpm"
            ? new ProcessResult(1, string.Empty, "failed")
            : new ProcessResult(0, string.Empty, string.Empty));
        UnitTestCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        int exitCode = await ((ICommand<TestSettings>)command).ExecuteAsync(
            context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_TestInvocation_UsesARelativeResultsDirectory()
    {
        // Uses a relative path - under --tools container, an absolute host path makes
        // the test host try to create it inside the container's filesystem instead of
        // under /workspace.
        FakeToolExecutor toolExecutor = new();
        UnitTestCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        await ((ICommand<TestSettings>)command).ExecuteAsync(context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        ToolInvocation testInvocation = Assert.Single(toolExecutor.Invocations, i => i.Tool == "dotnet" && i.Arguments.Contains("test"));
        int index = testInvocation.Arguments.ToList().IndexOf("--results-directory");
        Assert.True(index >= 0, "Expected a --results-directory argument.");
        Assert.False(Path.IsPathRooted(testInvocation.Arguments[index + 1]));
    }

    [Fact]
    public async Task ExecuteAsync_RunsBackendAndFrontendConcurrently_NotSequentially()
    {
        FakeToolExecutor toolExecutor = new(
            delaySelector: invocation => invocation.Tool == "dotnet" && invocation.Arguments.Contains("test") || invocation.Tool == "pnpm"
                ? TimeSpan.FromMilliseconds(200)
                : TimeSpan.Zero);
        UnitTestCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        var stopwatch = Stopwatch.StartNew();
        await ((ICommand<TestSettings>)command).ExecuteAsync(
            context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Both branches wait 200ms - sequential would take ~400ms+; concurrent stays close to 200ms.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(350), $"Expected concurrent execution, took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task ExecuteAsync_StaleTrxFileExists_DeletesItBeforeRunning()
    {
        string tempRoot = Directory.CreateTempSubdirectory("eshop-unit-test-command-").FullName;
        try
        {
            RepoPaths paths = new(tempRoot);
            Directory.CreateDirectory(paths.TestResultsDir);
            string staleTrx = Path.Combine(paths.TestResultsDir, "stale.trx");
            await File.WriteAllTextAsync(staleTrx, "<stale/>", TestContext.Current.CancellationToken);

            FakeToolExecutor toolExecutor = new();
            UnitTestCommand command = new(new RecordingOutputSink(), toolExecutor, paths);

            await ((ICommand<TestSettings>)command).ExecuteAsync(
                context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

            Assert.False(File.Exists(staleTrx));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
