// <copyright file="RestoreCommandTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Commands;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands;

public sealed class RestoreCommandTests
{
    [Fact]
    public async Task ExecuteAsync_AllStepsSucceed_ReturnsZero()
    {
        FakeToolExecutor toolExecutor = new();
        RestoreCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(toolExecutor.Invocations, i => i.Tool == "dotnet" && i.Arguments.Contains("restore"));
        Assert.Contains(toolExecutor.Invocations, i => i.Tool == "dotnet" && i.Arguments.SequenceEqual(["tool", "restore"]));
        Assert.Contains(toolExecutor.Invocations, i => i.Tool == "pnpm" && i.Arguments.Contains("install"));
    }

    [Fact]
    public async Task ExecuteAsync_SolutionRestoreFails_ReturnsNonZeroAndSearchesTheBinlog()
    {
        FakeToolExecutor toolExecutor = new(invocation => invocation.Arguments.Contains("restore") && !invocation.Arguments.Contains("tool")
            ? new ProcessResult(1, string.Empty, "restore failed")
            : new ProcessResult(0, string.Empty, string.Empty));
        RestoreCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains(toolExecutor.Invocations, i => i.Arguments.Contains("binlogtool"));
    }

    [Fact]
    public async Task ExecuteAsync_SolutionRestoreSucceeds_DoesNotSearchTheBinlog()
    {
        FakeToolExecutor toolExecutor = new();
        RestoreCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        await ((ICommand<DefaultSettings>)command).ExecuteAsync(context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(toolExecutor.Invocations, i => i.Arguments.Contains("binlogtool"));
    }

    [Fact]
    public async Task ExecuteAsync_ToolRestoreFails_ReturnsNonZero()
    {
        FakeToolExecutor toolExecutor = new(invocation => invocation.Arguments.SequenceEqual(["tool", "restore"])
            ? new ProcessResult(1, string.Empty, "tool restore failed")
            : new ProcessResult(0, string.Empty, string.Empty));
        RestoreCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_PnpmInstallFails_ReturnsNonZero()
    {
        FakeToolExecutor toolExecutor = new(invocation => invocation.Tool == "pnpm"
            ? new ProcessResult(1, string.Empty, "pnpm failed")
            : new ProcessResult(0, string.Empty, string.Empty));
        RestoreCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_RunsIndependentStepsConcurrently_NotSequentially()
    {
        FakeToolExecutor toolExecutor = new(
            delaySelector: invocation =>
                (invocation.Tool == "dotnet" && invocation.Arguments.Contains("restore") && !invocation.Arguments.Contains("tool"))
                || invocation.Tool == "pnpm"
                    ? TimeSpan.FromMilliseconds(200)
                    : TimeSpan.Zero);
        RestoreCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        var stopwatch = Stopwatch.StartNew();
        await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Two branches each wait 200ms - sequential would take ~400ms+; concurrent stays close to 200ms.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(350), $"Expected concurrent execution, took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task ExecuteAsync_RestoreInvocation_WritesBinlogUnderTmp()
    {
        FakeToolExecutor toolExecutor = new();
        RestoreCommand command = new(new RecordingOutputSink(), toolExecutor, new RepoPaths("/repo"));

        await ((ICommand<DefaultSettings>)command).ExecuteAsync(context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        ToolInvocation restoreInvocation = Assert.Single(
            toolExecutor.Invocations, i => i.Tool == "dotnet" && i.Arguments.Contains("restore") && !i.Arguments.Contains("tool"));
        Assert.Contains(restoreInvocation.Arguments, arg => arg.StartsWith("-bl:", StringComparison.Ordinal) && arg.Contains("tmp", StringComparison.Ordinal));
        Assert.DoesNotContain(
            restoreInvocation.Arguments,
            arg => arg.StartsWith("-bl:", StringComparison.Ordinal) && Path.IsPathRooted(arg["-bl:".Length..]));
    }

    [Fact]
    public async Task ExecuteAsync_RestoreSucceedsWithLeftoverBinlog_DeletesIt()
    {
        string tempRoot = Directory.CreateTempSubdirectory("eshop-restore-command-").FullName;
        try
        {
            RepoPaths paths = new(tempRoot);
            Directory.CreateDirectory(paths.TmpDir);
            string binlogPath = Path.Combine(paths.TmpDir, "restore.binlog");
            await File.WriteAllTextAsync(binlogPath, "stale", TestContext.Current.CancellationToken);

            FakeToolExecutor toolExecutor = new();
            RestoreCommand command = new(new RecordingOutputSink(), toolExecutor, paths);

            await ((ICommand<DefaultSettings>)command).ExecuteAsync(context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

            Assert.False(File.Exists(binlogPath));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
