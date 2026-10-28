// <copyright file="ProcessRunnerTests.cs" company="Henrik Jensen">
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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Hj.EShop.Cli.Execution;
using Xunit;

namespace Hj.EShop.Cli.Tests.Execution;

// Uses the required local dotnet executable, avoiding Docker and network dependencies.
public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_CapturesStandardOutputAndExitCode()
    {
        ProcessRunner runner = new();
        ProcessRequest request = new("dotnet", ["--version"]);

        ProcessResult result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.StandardOutput.Trim());
    }

    [Fact]
    public async Task RunAsync_NonZeroExitCode_IsNotSucceeded()
    {
        ProcessRunner runner = new();
        ProcessRequest request = new("dotnet", ["some-unknown-verb-that-does-not-exist"]);

        ProcessResult result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_OutputLineHandler_StreamsBothOutputStreamsAndCapturesThem()
    {
        ProcessRunner runner = new();
        ConcurrentQueue<ProcessOutputLine> received = [];
        ProcessRequest request = new(
            "sh",
            ["-c", "printf 'standard output\\n'; printf 'standard error\\n' >&2"],
            OutputLineHandler: received.Enqueue);

        ProcessResult result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains(new ProcessOutputLine(ProcessOutputStream.StandardOutput, "standard output"), received);
        Assert.Contains(new ProcessOutputLine(ProcessOutputStream.StandardError, "standard error"), received);
        Assert.Equal("standard output", result.StandardOutput.Trim());
        Assert.Equal("standard error", result.StandardError.Trim());
    }

    [Fact]
    public async Task RunAsync_CaptureLimit_KeepsTailAndStillStreamsEveryLine()
    {
        ProcessRunner runner = new();
        ConcurrentQueue<ProcessOutputLine> received = [];
        ProcessRequest request = new(
            "sh",
            ["-c", "i=0; while [ \"$i\" -lt 1000 ]; do printf 'output-%s\\n' \"$i\"; printf 'error-%s\\n' \"$i\" >&2; i=$((i + 1)); done"],
            OutputLineHandler: received.Enqueue,
            MaxCapturedOutputCharacters: 128);

        ProcessResult result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.StandardOutput.Length <= 128);
        Assert.True(result.StandardError.Length <= 128);
        Assert.Contains("output-999", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("error-999", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("output-0\\n", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(2000, received.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RunAsync_InvalidCaptureLimit_ThrowsBeforeStartingProcess(int limit)
    {
        ProcessRunner runner = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => runner.RunAsync(
                new ProcessRequest("no-such-process", [], MaxCapturedOutputCharacters: limit),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelled_DoesNotStartProcess()
    {
        ProcessRunner runner = new();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(new ProcessRequest("no-such-process", []), cancellation.Token));
    }

    [Fact]
    public async Task RunAsync_CancelledNoisyProcess_DrainsCappedOutputAndTerminates()
    {
        ProcessRunner runner = new();
        TaskCompletionSource firstOutput = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource cancellation = new();
        ProcessRequest request = new(
            "sh", ["-c", "while :; do printf 'repeated diagnostic\\n' >&2; done"],
            OutputLineHandler: _ => firstOutput.TrySetResult(),
            MaxCapturedOutputCharacters: 128);
        Task<ProcessResult> runTask = runner.RunAsync(request, cancellation.Token);

        await firstOutput.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task RunAsync_Cancelled_KillsTheChildProcess()
    {
        ProcessRunner runner = new();
        string pidFilePath = Path.Combine(Path.GetTempPath(), $"eshop-cli-test-{Guid.NewGuid():N}.pid");

        try
        {
            using CancellationTokenSource cts = new();
            ProcessRequest request = new("sh", ["-c", $"echo $$ > '{pidFilePath}'; exec sleep 30"]);
            Task<ProcessResult> runTask = runner.RunAsync(request, cts.Token);

            int childPid = await WaitForPidFileAsync(pidFilePath, TestContext.Current.CancellationToken);
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
            Assert.Throws<ArgumentException>(() => Process.GetProcessById(childPid));
        }
        finally
        {
            File.Delete(pidFilePath);
        }
    }

    // sh writes its own PID before `exec`-ing into sleep, so the file always names the
    // exact OS process ProcessRunner is tracking.
    private static async Task<int> WaitForPidFileAsync(string path, CancellationToken cancellationToken)
    {
        while (!File.Exists(path))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }

        return int.Parse((await File.ReadAllTextAsync(path, cancellationToken)).Trim(), CultureInfo.InvariantCulture);
    }
}
