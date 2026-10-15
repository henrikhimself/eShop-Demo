// <copyright file="RunCommandTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Commands;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands;

public sealed class RunCommandTests
{
    [Fact]
    public async Task ExecuteAsync_AlreadyRunning_FailsWithoutStartingAnything()
    {
        FakeAppHostSessionRunner sessionRunner = new();
        FakeContainerRunner containerRunner = new();
        RunCommand command = CreateCommand(new FakeAppHostGuard(alreadyRunning: true), new FakeLocalToolLocator("aspire"), sessionRunner, containerRunner);

        int exitCode = await ((ICommand<RunSettings>)command).ExecuteAsync(
            context: null!, settings: new RunSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.False(sessionRunner.WasCalled);
        Assert.Empty(containerRunner.Invocations);
    }

    [Fact]
    public async Task ExecuteAsync_LocalAspireOnPath_UsesNativeSessionRunner()
    {
        FakeAppHostSessionRunner sessionRunner = new();
        FakeContainerRunner containerRunner = new();
        RunCommand command = CreateCommand(new FakeAppHostGuard(alreadyRunning: false), new FakeLocalToolLocator("aspire"), sessionRunner, containerRunner);

        int exitCode = await ((ICommand<RunSettings>)command).ExecuteAsync(
            context: null!, settings: new RunSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.True(sessionRunner.WasCalled);
        Assert.Empty(containerRunner.Invocations);
    }

    [Fact]
    public async Task ExecuteAsync_NoLocalAspire_RunsContainerFallbackScript()
    {
        FakeAppHostSessionRunner sessionRunner = new();
        FakeContainerRunner containerRunner = new();
        RunCommand command = CreateCommand(new FakeAppHostGuard(alreadyRunning: false), new FakeLocalToolLocator(), sessionRunner, containerRunner);

        int exitCode = await ((ICommand<RunSettings>)command).ExecuteAsync(
            context: null!, settings: new RunSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.False(sessionRunner.WasCalled);
        ToolInvocation invocation = Assert.Single(containerRunner.Invocations);
        Assert.Equal("bash", invocation.Tool);
        Assert.True(invocation.Interactive);
    }

    [Fact]
    public async Task ExecuteAsync_LocalAspireSessionRunnerFails_PropagatesItsExitCode()
    {
        FakeAppHostSessionRunner sessionRunner = new(exitCode: 3);
        FakeContainerRunner containerRunner = new();
        RunCommand command = CreateCommand(new FakeAppHostGuard(alreadyRunning: false), new FakeLocalToolLocator("aspire"), sessionRunner, containerRunner);

        int exitCode = await ((ICommand<RunSettings>)command).ExecuteAsync(
            context: null!, settings: new RunSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(3, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ContainerFallbackScriptFails_PropagatesItsExitCode()
    {
        FakeAppHostSessionRunner sessionRunner = new();
        FakeContainerRunner containerRunner = new(_ => new ProcessResult(2, string.Empty, "container script failed"));
        RunCommand command = CreateCommand(new FakeAppHostGuard(alreadyRunning: false), new FakeLocalToolLocator(), sessionRunner, containerRunner);

        int exitCode = await ((ICommand<RunSettings>)command).ExecuteAsync(
            context: null!, settings: new RunSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
    }

    private static RunCommand CreateCommand(
        IAppHostGuard guard,
        FakeLocalToolLocator localToolLocator,
        FakeAppHostSessionRunner sessionRunner,
        FakeContainerRunner containerRunner)
    {
        return new RunCommand(
            new RecordingOutputSink(),
            guard,
            localToolLocator,
            sessionRunner,
            containerRunner,
            new FakeGlobalOptionsAccessor(new GlobalOptions(ExecutionMode.Auto, OutputMode.Human)),
            new RepoPaths("/repo"),
            new FakeRunSettingsReader());
    }

    private sealed class FakeAppHostGuard(bool alreadyRunning) : IAppHostGuard
    {
        public Task<bool> IsAlreadyRunningAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(alreadyRunning);
        }
    }

    private sealed class FakeAppHostSessionRunner(int exitCode = 0) : IAppHostSessionRunner
    {
        public bool WasCalled { get; private set; }

        public Task<int> RunAsync(IReadOnlyList<string> extraArguments, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(exitCode);
        }
    }
}
