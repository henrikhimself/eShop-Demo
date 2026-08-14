// <copyright file="ScreenshotCommandTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Commands;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands;

public sealed class ScreenshotCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("eshop-cli-tests-").FullName;

    [Fact]
    public async Task ExecuteAsync_AlwaysForcesContainerRegardlessOfGlobalTools()
    {
        RepoPaths paths = new(_root);
        string outputPath = Path.Combine(paths.Root, "tmp", "screenshot.png");
        FakeToolExecutor toolExecutor = new(invocation =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, [1, 2, 3]);
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        ScreenshotCommand command = new(new RecordingOutputSink(), toolExecutor, paths);
        ScreenshotSettings settings = new() { Url = "http://localhost:3000", Tools = ExecutionMode.Local };

        await ((ICommand<ScreenshotSettings>)command).ExecuteAsync(context: null!, settings, TestContext.Current.CancellationToken);

        ToolInvocation invocation = Assert.Single(toolExecutor.Invocations);
        Assert.Equal(ExecutionMode.Container, invocation.ForceMode);
    }

    [Fact]
    public async Task ExecuteAsync_RunsTheGeneratedNodeScript_WithUrlAndDimensionsAsArguments()
    {
        RepoPaths paths = new(_root);
        string outputPath = Path.Combine(paths.Root, "tmp", "screenshot.png");
        FakeToolExecutor toolExecutor = new(_ =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, [1, 2, 3]);
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        ScreenshotCommand command = new(new RecordingOutputSink(), toolExecutor, paths);
        ScreenshotSettings settings = new() { Url = "http://localhost:3000/drafts", Width = 640, Height = 480 };

        await ((ICommand<ScreenshotSettings>)command).ExecuteAsync(context: null!, settings, TestContext.Current.CancellationToken);

        ToolInvocation invocation = Assert.Single(toolExecutor.Invocations);
        Assert.Equal("node", invocation.Tool);
        Assert.Equal(Path.Combine("tmp", ScreenshotContainerScript.FileName), invocation.Arguments[0]);
        Assert.Contains("--url", invocation.Arguments);
        Assert.Contains("http://localhost:3000/drafts", invocation.Arguments);
        Assert.Contains("--width", invocation.Arguments);
        Assert.Contains("640", invocation.Arguments);
        Assert.Contains("--height", invocation.Arguments);
        Assert.Contains("480", invocation.Arguments);
        Assert.Contains("--profile-dir", invocation.Arguments);
        Assert.DoesNotContain("--login", invocation.Arguments);

        string scriptPath = Path.Combine(paths.TmpDir, ScreenshotContainerScript.FileName);
        Assert.True(File.Exists(scriptPath));
        Assert.Equal(ScreenshotContainerScript.Script, await File.ReadAllTextAsync(scriptPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_LoginRequested_PassesLoginUsernameAndPasswordArguments()
    {
        RepoPaths paths = new(_root);
        string outputPath = Path.Combine(paths.Root, "tmp", "screenshot.png");
        FakeToolExecutor toolExecutor = new(_ =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, [1, 2, 3]);
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        ScreenshotCommand command = new(new RecordingOutputSink(), toolExecutor, paths);
        ScreenshotSettings settings = new()
        {
            Url = "http://localhost:3000/drafts",
            Login = true,
            Username = "test-seller",
            Password = "TestSeller123!",
        };

        await ((ICommand<ScreenshotSettings>)command).ExecuteAsync(context: null!, settings, TestContext.Current.CancellationToken);

        ToolInvocation invocation = Assert.Single(toolExecutor.Invocations);
        Assert.Contains("--login", invocation.Arguments);
        Assert.Contains("--username", invocation.Arguments);
        Assert.Contains("test-seller", invocation.Arguments);
        Assert.Contains("--password", invocation.Arguments);
        Assert.Contains("TestSeller123!", invocation.Arguments);
    }

    [Fact]
    public async Task ExecuteAsync_LoginNotRequested_OmitsLoginUsernameAndPasswordArguments()
    {
        RepoPaths paths = new(_root);
        string outputPath = Path.Combine(paths.Root, "tmp", "screenshot.png");
        FakeToolExecutor toolExecutor = new(_ =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, [1, 2, 3]);
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        ScreenshotCommand command = new(new RecordingOutputSink(), toolExecutor, paths);
        ScreenshotSettings settings = new() { Url = "http://localhost:3000/drafts" };

        await ((ICommand<ScreenshotSettings>)command).ExecuteAsync(context: null!, settings, TestContext.Current.CancellationToken);

        ToolInvocation invocation = Assert.Single(toolExecutor.Invocations);
        Assert.DoesNotContain("--login", invocation.Arguments);
        Assert.DoesNotContain("--username", invocation.Arguments);
        Assert.DoesNotContain("--password", invocation.Arguments);
    }

    [Fact]
    public async Task ExecuteAsync_ToolSucceedsAndFileExists_ReturnsZeroAndReportsPath()
    {
        RepoPaths paths = new(_root);
        string outputPath = Path.Combine(paths.Root, "tmp", "screenshot.png");
        FakeToolExecutor toolExecutor = new(_ =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, [1, 2, 3]);
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        RecordingOutputSink output = new();
        ScreenshotCommand command = new(output, toolExecutor, paths);
        ScreenshotSettings settings = new() { Url = "http://localhost:3000" };

        int exitCode = await ((ICommand<ScreenshotSettings>)command).ExecuteAsync(
            context: null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(output.Calls, call => call.Contains(outputPath, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ToolFailsOrFileMissing_ReturnsNonZero()
    {
        RepoPaths paths = new(_root);
        FakeToolExecutor toolExecutor = new(_ => new ProcessResult(1, string.Empty, "chrome crashed"));
        ScreenshotCommand command = new(new RecordingOutputSink(), toolExecutor, paths);
        ScreenshotSettings settings = new() { Url = "http://localhost:3000" };

        int exitCode = await ((ICommand<ScreenshotSettings>)command).ExecuteAsync(
            context: null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_CustomOutputPath_CreatesMissingParentDirectory()
    {
        RepoPaths paths = new(_root);
        string outputPath = Path.Combine(paths.Root, "tmp", "nested", "shot.png");
        FakeToolExecutor toolExecutor = new(_ =>
        {
            File.WriteAllBytes(outputPath, [1]);
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        ScreenshotCommand command = new(new RecordingOutputSink(), toolExecutor, paths);
        ScreenshotSettings settings = new() { Url = "http://localhost:3000", OutputPath = "tmp/nested/shot.png" };

        int exitCode = await ((ICommand<ScreenshotSettings>)command).ExecuteAsync(
            context: null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(Path.Combine(paths.Root, "tmp", "nested")));
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
