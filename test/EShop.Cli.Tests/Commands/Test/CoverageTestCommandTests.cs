// <copyright file="CoverageTestCommandTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Commands.Test;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands.Test;

public sealed class CoverageTestCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("eshop-cli-tests-").FullName;

    [Fact]
    public async Task ExecuteAsync_TestRunSucceeds_ReturnsZero()
    {
        RepoPaths paths = new(_root);
        FakeToolExecutor toolExecutor = new(invocation =>
        {
            if (invocation.Arguments.Contains("reportgenerator"))
            {
                Directory.CreateDirectory(Path.Combine(paths.TmpDir, "coverage"));
                File.WriteAllText(Path.Combine(paths.TmpDir, "coverage", "SummaryGithub.md"), "# Coverage\n");
            }

            return new ProcessResult(0, string.Empty, string.Empty);
        });
        RecordingOutputSink output = new();
        CoverageTestCommand command = new(output, toolExecutor, paths);

        int exitCode = await ((ICommand<TestSettings>)command).ExecuteAsync(
            context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);

        // A pasteable file:// link to the HTML report, not the report dumped inline.
        Assert.Contains(output.Calls, call => call.Contains("Coverage report: file://", StringComparison.Ordinal));
        Assert.DoesNotContain(output.Calls, call => call.Contains("# Coverage", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ToolInvocations_UseRelativePathsForEveryFileArgument()
    {
        // Uses relative paths for tool arguments - under --tools container, absolute
        // host paths point outside the container's /workspace mount.
        RepoPaths paths = new(_root);
        FakeToolExecutor toolExecutor = new();
        CoverageTestCommand command = new(new RecordingOutputSink(), toolExecutor, paths);

        await ((ICommand<TestSettings>)command).ExecuteAsync(context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        ToolInvocation collect = Assert.Single(toolExecutor.Invocations, i => i.Arguments.Contains("dotnet-coverage"));
        List<string> collectArgs = [.. collect.Arguments];
        Assert.False(Path.IsPathRooted(collectArgs[collectArgs.IndexOf("-o") + 1]));
        Assert.False(Path.IsPathRooted(collectArgs[collectArgs.IndexOf("--results-directory") + 1]));

        ToolInvocation reportGenerator = Assert.Single(toolExecutor.Invocations, i => i.Arguments.Contains("reportgenerator"));
        Assert.DoesNotContain(reportGenerator.Arguments, arg => arg.StartsWith("-reports:", StringComparison.Ordinal) && Path.IsPathRooted(arg["-reports:".Length..]));
        Assert.DoesNotContain(reportGenerator.Arguments, arg => arg.StartsWith("-targetdir:", StringComparison.Ordinal) && Path.IsPathRooted(arg["-targetdir:".Length..]));
    }

    [Fact]
    public async Task ExecuteAsync_NoSummaryProduced_StillReturnsBasedOnTestExitCode()
    {
        RepoPaths paths = new(_root);
        FakeToolExecutor toolExecutor = new(invocation => invocation.Arguments.Contains("dotnet-coverage")
            ? new ProcessResult(1, string.Empty, "failed")
            : new ProcessResult(0, string.Empty, string.Empty));
        RecordingOutputSink output = new();
        CoverageTestCommand command = new(output, toolExecutor, paths);

        int exitCode = await ((ICommand<TestSettings>)command).ExecuteAsync(
            context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains(output.Calls, call => call.Contains("No coverage report was produced.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_StaleTrxFileExists_DeletesItBeforeRunning()
    {
        RepoPaths paths = new(_root);
        Directory.CreateDirectory(paths.TestResultsDir);
        string staleTrx = Path.Combine(paths.TestResultsDir, "stale.trx");
        await File.WriteAllTextAsync(staleTrx, "<stale/>", TestContext.Current.CancellationToken);

        FakeToolExecutor toolExecutor = new();
        CoverageTestCommand command = new(new RecordingOutputSink(), toolExecutor, paths);

        await ((ICommand<TestSettings>)command).ExecuteAsync(context: null!, settings: new TestSettings(), TestContext.Current.CancellationToken);

        Assert.False(File.Exists(staleTrx));
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
