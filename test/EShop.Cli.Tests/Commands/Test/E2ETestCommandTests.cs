// <copyright file="E2ETestCommandTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Commands.Test;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands.Test;

public sealed class E2ETestCommandTests
{
    [Fact]
    public async Task ExecuteAsync_AlreadyRunning_FailsWithoutRestoringOrTesting()
    {
        FakeToolExecutor toolExecutor = new();
        E2ETestCommand command = new(new RecordingOutputSink(), toolExecutor, new FakeAppHostGuard(alreadyRunning: true), new RepoPaths("/repo"));

        int exitCode = await ((ICommand<TestSettings>)command).ExecuteAsync(
            context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Empty(toolExecutor.Invocations);
    }

    [Fact]
    public async Task ExecuteAsync_RestoreAndTest_AlwaysForceContainerRegardlessOfGlobalTools()
    {
        FakeToolExecutor toolExecutor = new();
        E2ETestCommand command = new(new RecordingOutputSink(), toolExecutor, new FakeAppHostGuard(alreadyRunning: false), new RepoPaths("/repo"));

        await ((ICommand<TestSettings>)command).ExecuteAsync(context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        Assert.Contains(toolExecutor.Invocations, i => i.Arguments.Contains("restore") && i.ForceMode == ExecutionMode.Container);
        Assert.Contains(toolExecutor.Invocations, i => i.Arguments.Contains("test") && i.ForceMode == ExecutionMode.Container);
    }

    [Fact]
    public async Task ExecuteAsync_TestInvocation_UsesARelativeResultsDirectory()
    {
        // Uses a relative path - this invocation always runs inside the container,
        // where the repo is mounted at /workspace, not at the host's absolute path.
        FakeToolExecutor toolExecutor = new();
        E2ETestCommand command = new(new RecordingOutputSink(), toolExecutor, new FakeAppHostGuard(alreadyRunning: false), new RepoPaths("/repo"));

        await ((ICommand<TestSettings>)command).ExecuteAsync(context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        ToolInvocation testInvocation = Assert.Single(toolExecutor.Invocations, i => i.Arguments.Contains("test"));
        int index = testInvocation.Arguments.ToList().IndexOf("--results-directory");
        Assert.True(index >= 0, "Expected a --results-directory argument.");
        Assert.False(Path.IsPathRooted(testInvocation.Arguments[index + 1]));
    }

    [Fact]
    public async Task ExecuteAsync_TestFails_ReturnsNonZero()
    {
        FakeToolExecutor toolExecutor = new(invocation => invocation.Arguments.Contains("test")
            ? new ProcessResult(1, string.Empty, "failed")
            : new ProcessResult(0, string.Empty, string.Empty));
        E2ETestCommand command = new(new RecordingOutputSink(), toolExecutor, new FakeAppHostGuard(alreadyRunning: false), new RepoPaths("/repo"));

        int exitCode = await ((ICommand<TestSettings>)command).ExecuteAsync(
            context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_StaleTrxFileExists_DeletesItBeforeRunning()
    {
        string tempRoot = Directory.CreateTempSubdirectory("eshop-e2e-test-command-").FullName;
        try
        {
            RepoPaths paths = new(tempRoot);
            Directory.CreateDirectory(paths.TestResultsDir);
            string staleTrx = Path.Combine(paths.TestResultsDir, "stale.trx");
            await File.WriteAllTextAsync(staleTrx, "<stale/>", TestContext.Current.CancellationToken);

            FakeToolExecutor toolExecutor = new();
            E2ETestCommand command = new(new RecordingOutputSink(), toolExecutor, new FakeAppHostGuard(alreadyRunning: false), paths);

            await ((ICommand<TestSettings>)command).ExecuteAsync(
                context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

            Assert.False(File.Exists(staleTrx));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class FakeAppHostGuard(bool alreadyRunning) : IAppHostGuard
    {
        public Task<bool> IsAlreadyRunningAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(alreadyRunning);
        }
    }
}
