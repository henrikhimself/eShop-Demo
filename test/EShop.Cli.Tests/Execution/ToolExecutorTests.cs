// <copyright file="ToolExecutorTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Xunit;

namespace Hj.EShop.Cli.Tests.Execution;

public sealed class ToolExecutorTests
{
    [Fact]
    public async Task RunAsync_AutoAndToolOnPath_RunsLocally()
    {
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(new GlobalOptions(ExecutionMode.Auto, OutputMode.Human)),
            new AlwaysOnPathLocator(),
            containerRunner,
            processRunner,
            new RecordingOutputSink(),
            new RepoPaths("/repo"));

        await executor.RunAsync(new ToolInvocation("dotnet", ["build"]), TestContext.Current.CancellationToken);

        Assert.Single(processRunner.Invocations);
        Assert.Empty(containerRunner.Invocations);
    }

    [Fact]
    public async Task RunAsync_AutoAndToolMissing_RunsInContainer()
    {
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(new GlobalOptions(ExecutionMode.Auto, OutputMode.Human)),
            new NeverOnPathLocator(),
            containerRunner,
            processRunner,
            new RecordingOutputSink(),
            new RepoPaths("/repo"));

        await executor.RunAsync(new ToolInvocation("rumdl", ["check", "."]), TestContext.Current.CancellationToken);

        Assert.Empty(processRunner.Invocations);
        Assert.Single(containerRunner.Invocations);
    }

    [Fact]
    public async Task RunAsync_AutoAndToolOnPathButVersionMismatched_RunsInContainer()
    {
        // Locks down item 5's fix: a tool PrerequisiteChecker already flagged as a
        // version mismatch (e.g. Node 22 on PATH when .nvmrc pins 24) must fall back to
        // the container instead of silently running the wrong version locally.
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(
                new GlobalOptions(ExecutionMode.Auto, OutputMode.Human, new HashSet<string> { "node" })),
            new AlwaysOnPathLocator(),
            containerRunner,
            processRunner,
            new RecordingOutputSink(),
            new RepoPaths("/repo"));

        await executor.RunAsync(new ToolInvocation("node", ["--version"]), TestContext.Current.CancellationToken);

        Assert.Empty(processRunner.Invocations);
        Assert.Single(containerRunner.Invocations);
    }

    [Fact]
    public async Task RunAsync_AutoAndOtherToolVersionMismatched_StillRunsThisToolLocally()
    {
        // A version mismatch is per-tool: a mismatched "node" must not stop an
        // unrelated tool like "dotnet" from still running locally.
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(
                new GlobalOptions(ExecutionMode.Auto, OutputMode.Human, new HashSet<string> { "node" })),
            new AlwaysOnPathLocator(),
            containerRunner,
            processRunner,
            new RecordingOutputSink(),
            new RepoPaths("/repo"));

        await executor.RunAsync(new ToolInvocation("dotnet", ["build"]), TestContext.Current.CancellationToken);

        Assert.Single(processRunner.Invocations);
        Assert.Empty(containerRunner.Invocations);
    }

    [Fact]
    public async Task RunAsync_ModeLocal_RunsLocallyEvenWhenToolIsMissing()
    {
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(new GlobalOptions(ExecutionMode.Local, OutputMode.Human)),
            new NeverOnPathLocator(),
            containerRunner,
            processRunner,
            new RecordingOutputSink(),
            new RepoPaths("/repo"));

        await executor.RunAsync(new ToolInvocation("dotnet", ["build"]), TestContext.Current.CancellationToken);

        Assert.Single(processRunner.Invocations);
        Assert.Empty(containerRunner.Invocations);
    }

    [Fact]
    public async Task RunAsync_ModeContainer_RunsInContainerEvenWhenToolIsOnPath()
    {
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(new GlobalOptions(ExecutionMode.Container, OutputMode.Human)),
            new AlwaysOnPathLocator(),
            containerRunner,
            processRunner,
            new RecordingOutputSink(),
            new RepoPaths("/repo"));

        await executor.RunAsync(new ToolInvocation("dotnet", ["build"]), TestContext.Current.CancellationToken);

        Assert.Empty(processRunner.Invocations);
        Assert.Single(containerRunner.Invocations);
    }

    [Fact]
    public async Task RunAsync_ForceModeContainer_OverridesGlobalLocalOption()
    {
        // Locks down the `test e2e` contract: --tools local must not stop the
        // container-only e2e run from using the container.
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(new GlobalOptions(ExecutionMode.Local, OutputMode.Human)),
            new AlwaysOnPathLocator(),
            containerRunner,
            processRunner,
            new RecordingOutputSink(),
            new RepoPaths("/repo"));
        ToolInvocation invocation = new("dotnet", ["test"], ForceMode: ExecutionMode.Container);

        await executor.RunAsync(invocation, TestContext.Current.CancellationToken);

        Assert.Empty(processRunner.Invocations);
        ToolInvocation recorded = Assert.Single(containerRunner.Invocations);
        Assert.Equal(ExecutionMode.Container, recorded.ForceMode);
    }

    [Fact]
    public async Task RunAsync_OutputModeAi_SetsNoColorForALocalInvocation()
    {
        FakeProcessRunner processRunner = new();
        RecordingOutputSink output = new();
        RepoPaths paths = new("/repo");
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(new GlobalOptions(ExecutionMode.Local, OutputMode.Ai)),
            new AlwaysOnPathLocator(),
            new FakeContainerRunner(),
            processRunner,
            output,
            paths);

        await executor.RunAsync(new ToolInvocation("dotnet", ["build"]), TestContext.Current.CancellationToken);

        ProcessRequest request = Assert.Single(processRunner.Invocations);
        Assert.NotNull(request.EnvironmentVariables);
        Assert.Equal("1", request.EnvironmentVariables!["NO_COLOR"]);
        Assert.Equal(paths.CacheHomeDir, request.EnvironmentVariables["HOME"]);
    }

    [Fact]
    public async Task RunAsync_OutputModeHuman_ConfiguresRepositoryCacheEnvironment()
    {
        FakeProcessRunner processRunner = new();
        RecordingOutputSink output = new();
        RepoPaths paths = new("/repo");
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(new GlobalOptions(ExecutionMode.Local, OutputMode.Human)),
            new AlwaysOnPathLocator(),
            new FakeContainerRunner(),
            processRunner,
            output,
            paths);

        await executor.RunAsync(new ToolInvocation("dotnet", ["build"]), TestContext.Current.CancellationToken);

        ProcessRequest request = Assert.Single(processRunner.Invocations);
        Assert.NotNull(request.EnvironmentVariables);
        Assert.Equal(
            Path.Combine(paths.CacheHomeDir, ".aspnet", "dev-certs", "trust"),
            request.EnvironmentVariables!["SSL_CERT_DIR"]);
        Assert.Equal(paths.CacheHomeDir, request.EnvironmentVariables!["HOME"]);
        Assert.Equal(paths.CacheHomeDir, request.EnvironmentVariables["DOTNET_CLI_HOME"]);
        Assert.Equal(paths.NuGetPackagesDir, request.EnvironmentVariables["NUGET_PACKAGES"]);
        Assert.Equal(paths.NpmCacheDir, request.EnvironmentVariables["npm_config_cache"]);
        Assert.Equal(paths.PnpmStoreDir, request.EnvironmentVariables["PNPM_CONFIG_STORE_DIR"]);
        Assert.Equal(paths.PnpmHomeDir, request.EnvironmentVariables["PNPM_HOME"]);
        Assert.Equal("0", request.EnvironmentVariables["COREPACK_ENABLE_DOWNLOAD_PROMPT"]);
        Assert.DoesNotContain("NO_COLOR", request.EnvironmentVariables);
    }

    [Fact]
    public async Task RunAsync_OutputModeAi_SetsNoColorForAContainerInvocationToo()
    {
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(new GlobalOptions(ExecutionMode.Container, OutputMode.Ai)),
            new AlwaysOnPathLocator(),
            containerRunner,
            processRunner,
            new RecordingOutputSink(),
            new RepoPaths("/repo"));

        await executor.RunAsync(new ToolInvocation("dotnet", ["build"]), TestContext.Current.CancellationToken);

        ToolInvocation recorded = Assert.Single(containerRunner.Invocations);
        Assert.NotNull(recorded.EnvironmentVariables);
        Assert.Equal("1", recorded.EnvironmentVariables!["NO_COLOR"]);
    }

    [Fact]
    public async Task RunAsync_Debug_StreamsPrefixedLocalUtilityOutput()
    {
        FakeProcessRunner processRunner = new();
        RecordingOutputSink output = new();
        GlobalOptions options = new(ExecutionMode.Local, OutputMode.Human) { Debug = true };
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(options),
            new AlwaysOnPathLocator(),
            new FakeContainerRunner(),
            processRunner,
            output,
            new RepoPaths("/repo"));

        await executor.RunAsync(new ToolInvocation("pnpm", ["install"]), TestContext.Current.CancellationToken);

        ProcessRequest request = Assert.Single(processRunner.Invocations);
        Assert.NotNull(request.OutputLineHandler);
        request.OutputLineHandler!(new ProcessOutputLine(ProcessOutputStream.StandardError, "downloading package"));
        Assert.Contains("Debug(\"pnpm\", StandardError, \"downloading package\")", output.Calls);
    }

    [Fact]
    public async Task RunAsync_Debug_StreamsForcedContainerUtilityOutput()
    {
        FakeContainerRunner containerRunner = new();
        RecordingOutputSink output = new();
        GlobalOptions options = new(ExecutionMode.Local, OutputMode.Ai) { Debug = true };
        ToolExecutor executor = new(
            new FakeGlobalOptionsAccessor(options),
            new AlwaysOnPathLocator(),
            containerRunner,
            new FakeProcessRunner(),
            output,
            new RepoPaths("/repo"));

        await executor.RunAsync(
            new ToolInvocation("node", ["screenshot.mjs"], ForceMode: ExecutionMode.Container),
            TestContext.Current.CancellationToken);

        ToolInvocation invocation = Assert.Single(containerRunner.Invocations);
        Assert.NotNull(invocation.OutputLineHandler);
        invocation.OutputLineHandler!(new ProcessOutputLine(ProcessOutputStream.StandardOutput, "captured"));
        Assert.Contains("Debug(\"node\", StandardOutput, \"captured\")", output.Calls);
        Assert.Equal("1", invocation.EnvironmentVariables!["NO_COLOR"]);
    }

    private sealed class AlwaysOnPathLocator : ILocalToolLocator
    {
        public bool IsOnPath(string tool)
        {
            return true;
        }
    }

    private sealed class NeverOnPathLocator : ILocalToolLocator
    {
        public bool IsOnPath(string tool)
        {
            return false;
        }
    }
}
