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

// Uses the real "dotnet" on PATH rather than a Docker/network-dependent tool - dotnet
// is already a hard prerequisite for building/running this test suite at all, so this
// stays in the fast default run rather than moving to Integration/.
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
