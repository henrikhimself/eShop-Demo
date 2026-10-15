// <copyright file="BuildCommand.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Diagnostics;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;
using Spectre.Console.Cli;

namespace Hj.EShop.Cli.Commands;

// Never mutates files - see FormatCommand for that. Fails if the report is
// non-empty, including style-only diagnostics.
internal sealed class BuildCommand(
    IOutputSink output, IToolExecutor toolExecutor, RepoPaths paths, IBuildSettingsReader buildSettingsReader)
    : AsyncCommand<DefaultSettings>, IBuildRunner
{
    private const string FormatReportFileName = "build-format.json";

    public override Task<int> ExecuteAsync(CommandContext context, DefaultSettings settings, CancellationToken cancellationToken)
    {
        return RunAsync(cancellationToken);
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        output.Heading(1, "Build Result");

        CleanStaleArtifacts();

        string relativeSolution = Path.GetRelativePath(paths.Root, paths.Solution);
        string relativeWebDir = Path.GetRelativePath(paths.Root, paths.SellerPortalWebDir);

        NetBuildResult netBuild = await RunNetBuildAsync(relativeSolution, cancellationToken);
        IReadOnlyList<Diagnostic> buildDiagnostics = DiagnosticReport.Merge(netBuild.CompilerDiagnostics, netBuild.SarifDiagnostics);
        if (!netBuild.BuildResult.Succeeded)
        {
            if (buildDiagnostics.Count == 0)
            {
                buildDiagnostics = [CreateStepFailureDiagnostic("dotnet build", netBuild.BuildResult)];
            }

            await output.FlushPendingStepsAsync();
            output.Text(string.Join('\n', buildDiagnostics));
            output.Text("Build stopped because dotnet build failed; validation checks were not run.");
            output.Text($"Build finished with {buildDiagnostics.Count} errors.");
            return 1;
        }

        // These checks all depend on a successful .NET build or touch independent files,
        // so they can run concurrently once the build gate has passed.
        Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> formatTask =
            CollectFormatDiagnosticsAsync(relativeSolution, cancellationToken);
        Task<(bool Succeeded, IReadOnlyList<Diagnostic> Diagnostics)> schemaTask = CheckGeneratedSchemaAsync(cancellationToken);
        Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> efMigrationsTask = RunEfMigrationsCheckAsync(cancellationToken);
        Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> rumdlTask = RunRumdlAsync(cancellationToken);
        Task<(ProcessResult? Result, IReadOnlyList<Diagnostic> Diagnostics)> shellcheckTask = RunShellcheckAsync(cancellationToken);
        Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> lintTask = RunPnpmLintAsync(relativeWebDir, cancellationToken);
        Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> tscTask = RunPnpmTscAsync(relativeWebDir, cancellationToken);
        await Task.WhenAll(formatTask, schemaTask, efMigrationsTask, rumdlTask, shellcheckTask, lintTask, tscTask);

        // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
        await output.FlushPendingStepsAsync();

        (ProcessResult rumdlResult, IReadOnlyList<Diagnostic> rumdlDiagnostics) = rumdlTask.Result;
        (ProcessResult? shellcheckResult, IReadOnlyList<Diagnostic> shellcheckDiagnostics) = shellcheckTask.Result;
        (ProcessResult lintResult, IReadOnlyList<Diagnostic> lintDiagnostics) = lintTask.Result;
        (ProcessResult tscResult, IReadOnlyList<Diagnostic> tscDiagnostics) = tscTask.Result;

        IReadOnlyList<Diagnostic> allDiagnostics = DiagnosticReport.Merge(
            buildDiagnostics, formatTask.Result.Diagnostics, rumdlDiagnostics, shellcheckDiagnostics, lintDiagnostics,
            tscDiagnostics, schemaTask.Result.Diagnostics, efMigrationsTask.Result.Diagnostics);

        if (allDiagnostics.Count > 0)
        {
            output.Text(string.Join('\n', allDiagnostics));
        }

        output.Text($"Build finished with {allDiagnostics.Count} errors.");

        bool anyStepFailed = !formatTask.Result.Result.Succeeded || !rumdlResult.Succeeded || shellcheckResult is { Succeeded: false }
            || !lintResult.Succeeded || !tscResult.Succeeded || !schemaTask.Result.Succeeded || !efMigrationsTask.Result.Result.Succeeded;

        return allDiagnostics.Count > 0 || anyStepFailed ? 1 : 0;
    }

    // dotnet clean and dotnet build both touch the solution's obj/bin state, so they
    // must complete before the validation phase reads those artifacts.
    private async Task<NetBuildResult> RunNetBuildAsync(string relativeSolution, CancellationToken cancellationToken)
    {
        using (output.BeginStep("dotnet clean"))
        {
            // Best-effort - a clean failure shouldn't abort the whole report.
            await toolExecutor.RunAsync(
                new ToolInvocation("dotnet", ["clean", relativeSolution, "--nologo"], WorkingDirectory: paths.Root),
                cancellationToken);
        }

        // CLI-owned build parameters avoid changes to vendored projects.
        IReadOnlyList<string> extraBuildParams = await buildSettingsReader.GetBuildParamsAsync(cancellationToken);

        ProcessResult buildResult;
        using (output.BeginStep("dotnet build"))
        {
            buildResult = await toolExecutor.RunAsync(
                new ToolInvocation(
                    "dotnet",
                    [
                        "build", relativeSolution, "--nologo", "--no-incremental", "-warnaserror",
                        "/p:TreatWarningsAsErrors=true", "/p:RunAnalyzersDuringBuild=true",
                        .. extraBuildParams,
                    ],
                    WorkingDirectory: paths.Root),
                cancellationToken);
        }

        IReadOnlyList<Diagnostic> compilerDiagnostics =
            CompilerOutputParser.ParseDotnetBuildOutput(buildResult.StandardOutput + buildResult.StandardError);
        return new NetBuildResult(buildResult, compilerDiagnostics, CollectSarifDiagnostics());
    }

    // Catches the class of bug where a developer changes an EF entity/mapping but
    // forgets to add the corresponding migration. Runs only after a successful
    // solution build because --no-build reads the Bff project's compiled output.
    private async Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> RunEfMigrationsCheckAsync(CancellationToken cancellationToken)
    {
        string relativeBffProject = Path.GetRelativePath(paths.Root, paths.SellerPortalBffProject);
        ProcessResult result;
        using (output.BeginStep("dotnet ef migrations has-pending-model-changes"))
        {
            result = await toolExecutor.RunAsync(
                new ToolInvocation(
                    "dotnet",
                    ["ef", "migrations", "has-pending-model-changes", "--project", relativeBffProject, "--no-build"],
                    WorkingDirectory: paths.Root),
                cancellationToken);
        }

        if (result.Succeeded)
        {
            return (result, []);
        }

        string processOutput = (result.StandardOutput + result.StandardError).Trim();
        return processOutput.Contains("Changes have been made to the model since the last migration.", StringComparison.Ordinal)
            ? (
                result,
                [
                    new Diagnostic(
                        $"{relativeBffProject}(1,1): error: EF Core model has pending changes not captured by a "
                            + "migration - run `dotnet ef migrations add <Name>` and commit the result.\n"
                            + processOutput),
                ])
            : (result, [CreateStepFailureDiagnostic("EF Core migration model validation", result)]);
    }

    private async Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> RunRumdlAsync(CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep("rumdl check");
        ProcessResult result = await toolExecutor.RunAsync(
            new ToolInvocation("rumdl", ["check", "."], WorkingDirectory: paths.Root),
            cancellationToken);
        return (result, GccStyleOutputParser.Parse(result.StandardOutput + result.StandardError));
    }

    private async Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> RunPnpmLintAsync(
        string relativeWebDir, CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep("pnpm lint");
        ProcessResult result = await toolExecutor.RunAsync(
            new ToolInvocation("pnpm", ["--dir", relativeWebDir, "run", "lint", "--format", "unix"], WorkingDirectory: paths.Root),
            cancellationToken);
        return (result, GccStyleOutputParser.Parse(result.StandardOutput + result.StandardError));
    }

    private async Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> RunPnpmTscAsync(
        string relativeWebDir, CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep("pnpm tsc --noEmit");
        ProcessResult result = await toolExecutor.RunAsync(
            new ToolInvocation("pnpm", ["--dir", relativeWebDir, "exec", "tsc", "--noEmit"], WorkingDirectory: paths.Root),
            cancellationToken);
        return (result, CompilerOutputParser.ParseTypeScriptCompilerOutput(result.StandardOutput + result.StandardError));
    }

    private static Diagnostic CreateStepFailureDiagnostic(string stepName, ProcessResult result)
    {
        string output = (result.StandardOutput + result.StandardError).Trim();
        return new Diagnostic($"error: {stepName} failed.{(output.Length == 0 ? string.Empty : $"\n{output}")}");
    }

    private sealed record NetBuildResult(
        ProcessResult BuildResult,
        IReadOnlyList<Diagnostic> CompilerDiagnostics,
        IReadOnlyList<Diagnostic> SarifDiagnostics);

    private void CleanStaleArtifacts()
    {
        foreach (string sarifFile in Directory.EnumerateFiles(paths.Root, "*.sarif", SearchOption.AllDirectories))
        {
            File.Delete(sarifFile);
        }

        // Stale Coverlet source-root mapping files cause incremental build failures.
        foreach (string pattern in (string[])["CoverletSourceRootsMapping_*", "*.msCoverageSourceRootsMapping*"])
        {
            foreach (string file in Directory.EnumerateFiles(paths.Root, pattern, SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
        }
    }

    private IReadOnlyList<Diagnostic> CollectSarifDiagnostics()
    {
        IEnumerable<string> sarifDocuments = Directory
            .EnumerateFiles(paths.Root, "*.sarif", SearchOption.AllDirectories)
            .Select(File.ReadAllText);
        return SarifReportParser.Parse(sarifDocuments);
    }

    private async Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> CollectFormatDiagnosticsAsync(
        string relativeSolution, CancellationToken cancellationToken)
    {
        string reportPath = Path.Combine(paths.TmpDir, FormatReportFileName);

        // See doc/CHRONICLE.md - container tool arguments must stay repo-relative
        // (the absolute variant is still used below to read the report back).
        string relativeReportPath = Path.GetRelativePath(paths.Root, reportPath);

        ProcessResult result;
        using (output.BeginStep("dotnet format analyzers"))
        {
            result = await toolExecutor.RunAsync(
                new ToolInvocation(
                    "dotnet",
                    ["format", "analyzers", relativeSolution, "--verify-no-changes", "--report", relativeReportPath],
                    WorkingDirectory: paths.Root),
                cancellationToken);
        }

        if (!File.Exists(reportPath))
        {
            return (result, []);
        }

        string reportJson = await File.ReadAllTextAsync(reportPath, cancellationToken);
        return (result, DotnetFormatReportParser.Parse(reportJson));
    }

    private async Task<(ProcessResult? Result, IReadOnlyList<Diagnostic> Diagnostics)> RunShellcheckAsync(CancellationToken cancellationToken)
    {
        // Only *.sh scripts are checked - shellcheck only supports sh/bash/dash/ksh.
        string[] shellScripts =
        [
            .. Directory
                .EnumerateFiles(Path.Combine(paths.Root, "scripts"), "*.sh", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(paths.Root, file))
                .Order(StringComparer.Ordinal),
        ];

        if (shellScripts.Length == 0)
        {
            return (null, []);
        }

        using IDisposable step = output.BeginStep("shellcheck");
        ProcessResult result = await toolExecutor.RunAsync(
            new ToolInvocation("shellcheck", ["--format=gcc", .. shellScripts], WorkingDirectory: paths.Root),
            cancellationToken);
        return (result, GccStyleOutputParser.Parse(result.StandardOutput + result.StandardError));
    }

    private async Task<(bool Succeeded, IReadOnlyList<Diagnostic> Diagnostics)> CheckGeneratedSchemaAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.SellerPortalBffOpenApiJson))
        {
            return (
                false,
                [
                    new Diagnostic(
                        $"{paths.SellerPortalBffOpenApiJson}(1,1): error: not found - "
                            + "the Bff build above did not emit an OpenAPI document."),
                ]);
        }

        string scratchPath = Path.Combine(paths.TmpDir, "api-schema.d.ts");
        string relativeWebDir = Path.GetRelativePath(paths.Root, paths.SellerPortalWebDir);
        string openApiJsonFromWebDir = Path.GetRelativePath(paths.SellerPortalWebDir, paths.SellerPortalBffOpenApiJson);
        string scratchFromWebDir = Path.GetRelativePath(paths.SellerPortalWebDir, scratchPath);

        ProcessResult result;
        using (output.BeginStep("check generated API schema"))
        {
            result = await toolExecutor.RunAsync(
                new ToolInvocation(
                    "pnpm",
                    ["--dir", relativeWebDir, "exec", "openapi-typescript", openApiJsonFromWebDir, "-o", scratchFromWebDir],
                    WorkingDirectory: paths.Root),
                cancellationToken);
        }

        if (!result.Succeeded)
        {
            return (
                false,
                [
                    new Diagnostic(
                        $"{paths.SellerPortalBffOpenApiJson}(1,1): error: openapi-typescript failed - "
                            + (result.StandardOutput + result.StandardError).Trim()),
                ]);
        }

        string generated = await File.ReadAllTextAsync(scratchPath, cancellationToken);
        string committed = File.Exists(paths.GeneratedApiSchema)
            ? await File.ReadAllTextAsync(paths.GeneratedApiSchema, cancellationToken)
            : string.Empty;

        return generated == committed
            ? (true, [])
            : (true, [new Diagnostic($"{paths.GeneratedApiSchema}(1,1): error: {paths.GeneratedApiSchema} is stale - run `eshop generate types` and commit the result.")]);
    }
}
