// <copyright file="BuildCommandTests.cs" company="Henrik Jensen">
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

public sealed class BuildCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("eshop-cli-tests-").FullName;
    private readonly RepoPaths _paths;
    private readonly FakeBuildSettingsReader _buildSettingsReader = new();

    public BuildCommandTests()
    {
        _paths = new RepoPaths(_root);
        Directory.CreateDirectory(Path.Combine(_root, "scripts"));
        Directory.CreateDirectory(_paths.TmpDir);
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.SellerPortalBffOpenApiJson)!);
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.GeneratedApiSchema)!);
        File.WriteAllText(_paths.SellerPortalBffOpenApiJson, "{}");
    }

    [Fact]
    public async Task ExecuteAsync_EverythingClean_ReturnsZeroWithZeroErrorCount()
    {
        FakeToolExecutor toolExecutor = new(invocation =>
        {
            if (invocation.Tool == "pnpm" && invocation.Arguments.Contains("openapi-typescript"))
            {
                File.WriteAllText(Path.Combine(_paths.TmpDir, "api-schema.d.ts"), "committed content");
            }

            return new ProcessResult(0, string.Empty, string.Empty);
        });
        await File.WriteAllTextAsync(_paths.GeneratedApiSchema, "committed content", TestContext.Current.CancellationToken);
        RecordingOutputSink output = new();
        BuildCommand command = new(output, toolExecutor, _paths, _buildSettingsReader);

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(output.Calls, call => call.Contains("Build finished with 0 errors.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_CompilerErrorPresent_ReturnsNonZeroAndIncludesDiagnostic()
    {
        FakeToolExecutor toolExecutor = new(invocation =>
        {
            if (invocation.Tool == "dotnet" && invocation.Arguments.Contains("build"))
            {
                return new ProcessResult(1, "Foo.cs(1,1): error CS0001: broken\n", string.Empty);
            }

            if (invocation.Tool == "pnpm" && invocation.Arguments.Contains("openapi-typescript"))
            {
                File.WriteAllText(Path.Combine(_paths.TmpDir, "api-schema.d.ts"), "committed content");
            }

            return new ProcessResult(0, string.Empty, string.Empty);
        });
        await File.WriteAllTextAsync(_paths.GeneratedApiSchema, "committed content", TestContext.Current.CancellationToken);
        RecordingOutputSink output = new();
        BuildCommand command = new(output, toolExecutor, _paths, _buildSettingsReader);

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains(output.Calls, call => call.Contains("Foo.cs(1,1): error CS0001: broken", StringComparison.Ordinal));
        Assert.Contains(output.Calls, call => call.Contains("Build finished with 1 errors.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedSchemaStale_IsReportedAsADiagnostic()
    {
        FakeToolExecutor toolExecutor = new(invocation =>
        {
            if (invocation.Tool == "pnpm" && invocation.Arguments.Contains("openapi-typescript"))
            {
                File.WriteAllText(Path.Combine(_paths.TmpDir, "api-schema.d.ts"), "freshly generated content");
            }

            return new ProcessResult(0, string.Empty, string.Empty);
        });
        await File.WriteAllTextAsync(_paths.GeneratedApiSchema, "stale committed content", TestContext.Current.CancellationToken);
        RecordingOutputSink output = new();
        BuildCommand command = new(output, toolExecutor, _paths, _buildSettingsReader);

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains(output.Calls, call => call.Contains("is stale", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_SchemaRegenerationFails_IsReportedAsADiagnosticWithProcessOutput()
    {
        FakeToolExecutor toolExecutor = new(invocation =>
        {
            if (invocation.Tool == "pnpm" && invocation.Arguments.Contains("openapi-typescript"))
            {
                return new ProcessResult(1, string.Empty, "TypeError: cannot parse OpenAPI document");
            }

            return new ProcessResult(0, string.Empty, string.Empty);
        });
        await File.WriteAllTextAsync(_paths.GeneratedApiSchema, "committed content", TestContext.Current.CancellationToken);
        RecordingOutputSink output = new();
        BuildCommand command = new(output, toolExecutor, _paths, _buildSettingsReader);

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains(
            output.Calls, call => call.Contains("TypeError: cannot parse OpenAPI document", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_EfMigrationsPending_IsReportedAsADiagnosticWithProcessOutput()
    {
        FakeToolExecutor toolExecutor = new(invocation =>
        {
            if (invocation.Tool == "pnpm" && invocation.Arguments.Contains("openapi-typescript"))
            {
                File.WriteAllText(Path.Combine(_paths.TmpDir, "api-schema.d.ts"), "committed content");
            }

            if (invocation.Tool == "dotnet" && invocation.Arguments.Contains("has-pending-model-changes"))
            {
                return new ProcessResult(1, "Changes have been made to the model since the last migration.\n", string.Empty);
            }

            return new ProcessResult(0, string.Empty, string.Empty);
        });
        await File.WriteAllTextAsync(_paths.GeneratedApiSchema, "committed content", TestContext.Current.CancellationToken);
        RecordingOutputSink output = new();
        BuildCommand command = new(output, toolExecutor, _paths, _buildSettingsReader);

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains(
            output.Calls,
            call => call.Contains("EF Core model has pending changes", StringComparison.Ordinal)
                && call.Contains("Changes have been made to the model since the last migration.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_RunsIndependentToolsConcurrently_NotSequentially()
    {
        FakeToolExecutor toolExecutor = new(
            invocation =>
            {
                if (invocation.Tool == "pnpm" && invocation.Arguments.Contains("openapi-typescript"))
                {
                    File.WriteAllText(Path.Combine(_paths.TmpDir, "api-schema.d.ts"), "committed content");
                }

                return new ProcessResult(0, string.Empty, string.Empty);
            },
            invocation => invocation.Tool == "rumdl" || (invocation.Tool == "dotnet" && invocation.Arguments.Contains("build"))
                ? TimeSpan.FromMilliseconds(200)
                : TimeSpan.Zero);
        await File.WriteAllTextAsync(_paths.GeneratedApiSchema, "committed content", TestContext.Current.CancellationToken);
        BuildCommand command = new(new RecordingOutputSink(), toolExecutor, _paths, _buildSettingsReader);

        var stopwatch = Stopwatch.StartNew();
        await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Two branches each wait 200ms - sequential would take ~400ms+; concurrent stays close to 200ms.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(350), $"Expected concurrent execution, took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task ExecuteAsync_FormatReportArgument_IsRelativeNotAbsolute()
    {
        // Uses a relative path - under --tools container, an absolute host path makes
        // dotnet format write inside the container's filesystem instead of under
        // /workspace.
        FakeToolExecutor toolExecutor = new(invocation =>
        {
            if (invocation.Tool == "pnpm" && invocation.Arguments.Contains("openapi-typescript"))
            {
                File.WriteAllText(Path.Combine(_paths.TmpDir, "api-schema.d.ts"), "committed content");
            }

            return new ProcessResult(0, string.Empty, string.Empty);
        });
        await File.WriteAllTextAsync(_paths.GeneratedApiSchema, "committed content", TestContext.Current.CancellationToken);
        BuildCommand command = new(new RecordingOutputSink(), toolExecutor, _paths, _buildSettingsReader);

        await ((ICommand<DefaultSettings>)command).ExecuteAsync(context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        ToolInvocation formatInvocation = Assert.Single(
            toolExecutor.Invocations, i => i.Tool == "dotnet" && i.Arguments.Contains("format"));
        int index = formatInvocation.Arguments.ToList().IndexOf("--report");
        Assert.True(index >= 0, "Expected a --report argument.");
        Assert.False(Path.IsPathRooted(formatInvocation.Arguments[index + 1]));
    }

    [Fact]
    public async Task ExecuteAsync_FormatStepFailsWithNoDiagnostics_StillFailsTheBuild()
    {
        // Simulates the container-path mismatch bug this guards against: dotnet
        // format exits non-zero but never writes the report file, so the diagnostics
        // list would otherwise stay empty and silently mask the failure.
        FakeToolExecutor toolExecutor = new(invocation =>
        {
            if (invocation.Tool == "dotnet" && invocation.Arguments.Contains("format"))
            {
                return new ProcessResult(1, string.Empty, "could not write report");
            }

            if (invocation.Tool == "pnpm" && invocation.Arguments.Contains("openapi-typescript"))
            {
                File.WriteAllText(Path.Combine(_paths.TmpDir, "api-schema.d.ts"), "committed content");
            }

            return new ProcessResult(0, string.Empty, string.Empty);
        });
        await File.WriteAllTextAsync(_paths.GeneratedApiSchema, "committed content", TestContext.Current.CancellationToken);
        RecordingOutputSink output = new();
        BuildCommand command = new(output, toolExecutor, _paths, _buildSettingsReader);

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
