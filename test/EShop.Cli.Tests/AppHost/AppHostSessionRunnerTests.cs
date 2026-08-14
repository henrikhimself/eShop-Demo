// <copyright file="AppHostSessionRunnerTests.cs" company="Henrik Jensen">
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

// No matter which of 'q', EOF, or Ctrl+C ends the session, `aspire stop` must be
// issued exactly once.
public sealed class AppHostSessionRunnerTests
{
    [Fact]
    public async Task RunAsync_QPressed_StartsThenStopsExactlyOnce()
    {
        FakeToolExecutor toolExecutor = new();
        AppHostSessionRunner runner = new(new RecordingOutputSink(), toolExecutor, new FakeConsoleKeyReader('x', 'q'), new RepoPaths("/repo"));

        await runner.RunAsync([], TestContext.Current.CancellationToken);

        Assert.Equal(["start", "stop"], toolExecutor.Invocations.Select(i => i.Arguments[0]));
    }

    [Fact]
    public async Task RunAsync_EndOfInput_DoesNotStopUntilCancellation()
    {
        // EOF (e.g. stdin redirected from /dev/null under a non-interactive agent
        // harness) must not be treated the same as pressing 'q' - the AppHost stays
        // up until an explicit cancellation (Ctrl+C/SIGTERM) arrives.
        FakeToolExecutor toolExecutor = new();
        AppHostSessionRunner runner = new(new RecordingOutputSink(), toolExecutor, new FakeConsoleKeyReader((char?)null), new RepoPaths("/repo"));
        using CancellationTokenSource cancellation = new();
        Task<int> runTask = runner.RunAsync([], cancellation.Token);

        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        Assert.Equal(["start"], toolExecutor.Invocations.Select(i => i.Arguments[0]));

        await cancellation.CancelAsync();
        await runTask;

        Assert.Equal(["start", "stop"], toolExecutor.Invocations.Select(i => i.Arguments[0]));
    }

    [Fact]
    public async Task RunAsync_CancellationRequested_StopsExactlyOnceAndDoesNotThrow()
    {
        FakeToolExecutor toolExecutor = new();
        AppHostSessionRunner runner = new(new RecordingOutputSink(), toolExecutor, new FakeConsoleKeyReader(), new RepoPaths("/repo"));
        using CancellationTokenSource cancellation = new();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        await runner.RunAsync([], cancellation.Token);

        Assert.Equal(["start", "stop"], toolExecutor.Invocations.Select(i => i.Arguments[0]));
    }

    [Fact]
    public async Task RunAsync_AspireStartFails_ReturnsStartExitCodeWithoutWaitingOrStopping()
    {
        FakeToolExecutor toolExecutor = new(invocation => invocation.Arguments.Contains("start")
            ? new ProcessResult(3, string.Empty, "aspire: failed to start AppHost")
            : new ProcessResult(0, string.Empty, string.Empty));
        AppHostSessionRunner runner = new(new RecordingOutputSink(), toolExecutor, new FakeConsoleKeyReader('q'), new RepoPaths("/repo"));

        int exitCode = await runner.RunAsync([], TestContext.Current.CancellationToken);

        Assert.Equal(3, exitCode);
        Assert.Equal(["start"], toolExecutor.Invocations.Select(i => i.Arguments[0]));
    }

    [Fact]
    public async Task RunAsync_QPressedAndAspireStopFails_ReturnsStopExitCode()
    {
        FakeToolExecutor toolExecutor = new(invocation => invocation.Arguments.Contains("stop")
            ? new ProcessResult(4, string.Empty, "aspire: failed to stop AppHost")
            : new ProcessResult(0, string.Empty, string.Empty));
        AppHostSessionRunner runner = new(new RecordingOutputSink(), toolExecutor, new FakeConsoleKeyReader('q'), new RepoPaths("/repo"));

        int exitCode = await runner.RunAsync([], TestContext.Current.CancellationToken);

        Assert.Equal(4, exitCode);
    }

    [Fact]
    public async Task RunAsync_PrintsAspireStartsCapturedOutput()
    {
        FakeToolExecutor toolExecutor = new(invocation => invocation.Arguments.Contains("start")
            ? new ProcessResult(0, "Dashboard: https://localhost:12345/login?t=abc\n", string.Empty)
            : new ProcessResult(0, string.Empty, string.Empty));
        RecordingOutputSink output = new();
        AppHostSessionRunner runner = new(output, toolExecutor, new FakeConsoleKeyReader('q'), new RepoPaths("/repo"));

        await runner.RunAsync([], TestContext.Current.CancellationToken);

        Assert.Contains(output.Calls, call => call.Contains("Dashboard: https://localhost:12345/login?t=abc", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_ExtraArguments_ForwardedToAspireStart()
    {
        FakeToolExecutor toolExecutor = new();
        AppHostSessionRunner runner = new(new RecordingOutputSink(), toolExecutor, new FakeConsoleKeyReader('q'), new RepoPaths("/repo"));

        await runner.RunAsync(["--launch-profile", "https"], TestContext.Current.CancellationToken);

        Assert.Contains(toolExecutor.Invocations[0].Arguments, arg => arg == "--launch-profile");
    }
}
