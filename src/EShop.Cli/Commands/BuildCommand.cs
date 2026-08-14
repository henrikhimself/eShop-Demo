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
internal sealed class BuildCommand(IOutputSink output, IToolExecutor toolExecutor, RepoPaths paths)
    : AsyncCommand<DefaultSettings>
{
    private const string FormatReportFileName = "build-format.json";

    protected override async Task<int> ExecuteAsync(CommandContext context, DefaultSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "Build Result");

        CleanStaleArtifacts();

        string relativeSolution = Path.GetRelativePath(paths.Root, paths.Solution);
        string relativeWebDir = Path.GetRelativePath(paths.Root, paths.SellerPortalWebDir);

        // None of these five tasks touch each other's files, so they can run
        // concurrently.
        Task<NetChainResult> netChainTask = RunNetChainAsync(relativeSolution, cancellationToken);
        Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> rumdlTask = RunRumdlAsync(cancellationToken);
        Task<(ProcessResult? Result, IReadOnlyList<Diagnostic> Diagnostics)> shellcheckTask = RunShellcheckAsync(cancellationToken);
        Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> lintTask = RunPnpmLintAsync(relativeWebDir, cancellationToken);
        Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> tscTask = RunPnpmTscAsync(relativeWebDir, cancellationToken);
        await Task.WhenAll(netChainTask, rumdlTask, shellcheckTask, lintTask, tscTask);

        // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
        await output.FlushPendingStepsAsync();

        NetChainResult netChain = netChainTask.Result;
        (ProcessResult rumdlResult, IReadOnlyList<Diagnostic> rumdlDiagnostics) = rumdlTask.Result;
        (ProcessResult? shellcheckResult, IReadOnlyList<Diagnostic> shellcheckDiagnostics) = shellcheckTask.Result;
        (ProcessResult lintResult, IReadOnlyList<Diagnostic> lintDiagnostics) = lintTask.Result;
        (ProcessResult tscResult, IReadOnlyList<Diagnostic> tscDiagnostics) = tscTask.Result;

        IReadOnlyList<Diagnostic> allDiagnostics = DiagnosticReport.Merge(
            netChain.CompilerDiagnostics, netChain.SarifDiagnostics, netChain.FormatDiagnostics, rumdlDiagnostics,
            shellcheckDiagnostics, lintDiagnostics, tscDiagnostics, netChain.SchemaDiagnostics, netChain.EfMigrationsDiagnostics);

        if (allDiagnostics.Count > 0)
        {
            output.Text(string.Join('\n', allDiagnostics));
        }

        output.Text($"Build finished with {allDiagnostics.Count} errors.");

        bool anyStepFailed = !netChain.BuildResult.Succeeded || !netChain.FormatResult.Succeeded || !rumdlResult.Succeeded
            || shellcheckResult is { Succeeded: false } || !lintResult.Succeeded || !tscResult.Succeeded || !netChain.SchemaCheckSucceeded
            || !netChain.EfMigrationsResult.Succeeded;

        return allDiagnostics.Count > 0 || anyStepFailed ? 1 : 0;
    }

    // dotnet clean -> dotnet build -> [dotnet format analyzers || the schema check] -
    // format and the schema check only need build's output, not each other's, but
    // clean/build/format all touch the same solution's obj/bin state and can't run
    // alongside each other safely.
    private async Task<NetChainResult> RunNetChainAsync(string relativeSolution, CancellationToken cancellationToken)
    {
        using (output.BeginStep("dotnet clean"))
        {
            // Best-effort - a clean failure shouldn't abort the whole report.
            await toolExecutor.RunAsync(
                new ToolInvocation("dotnet", ["clean", relativeSolution, "--nologo"], WorkingDirectory: paths.Root),
                cancellationToken);
        }

        ProcessResult buildResult;
        using (output.BeginStep("dotnet build"))
        {
            buildResult = await toolExecutor.RunAsync(
                new ToolInvocation(
                    "dotnet",
                    [
                        "build", relativeSolution, "--nologo", "--no-incremental", "-warnaserror",
                        "/p:TreatWarningsAsErrors=true", "/p:RunAnalyzersDuringBuild=true",
                    ],
                    WorkingDirectory: paths.Root),
                cancellationToken);
        }

        IReadOnlyList<Diagnostic> compilerDiagnostics =
            CompilerOutputParser.ParseDotnetBuildOutput(buildResult.StandardOutput + buildResult.StandardError);
        IReadOnlyList<Diagnostic> sarifDiagnostics = CollectSarifDiagnostics();

        Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> formatTask =
            CollectFormatDiagnosticsAsync(relativeSolution, cancellationToken);
        Task<(bool Succeeded, IReadOnlyList<Diagnostic> Diagnostics)> schemaTask = CheckGeneratedSchemaAsync(cancellationToken);
        Task<(ProcessResult Result, IReadOnlyList<Diagnostic> Diagnostics)> efMigrationsTask = RunEfMigrationsCheckAsync(cancellationToken);
        await Task.WhenAll(formatTask, schemaTask, efMigrationsTask);

        return new NetChainResult(
            buildResult, compilerDiagnostics, sarifDiagnostics, formatTask.Result.Result, formatTask.Result.Diagnostics,
            schemaTask.Result.Succeeded, schemaTask.Result.Diagnostics,
            efMigrationsTask.Result.Result, efMigrationsTask.Result.Diagnostics);
    }

    // Catches the class of bug where a developer changes an EF entity/mapping but
    // forgets to add the corresponding migration. Runs after the solution build (not
    // alongside `dotnet clean`) since it triggers its own build of the Bff project
    // and would otherwise race against clean deleting that project's obj/bin.
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

        return result.Succeeded
            ? (result, [])
            : (
                result,
                [
                    new Diagnostic(
                        $"{relativeBffProject}(1,1): error: EF Core model has pending changes not captured by a "
                            + "migration - run `dotnet ef migrations add <Name>` and commit the result.\n"
                            + (result.StandardOutput + result.StandardError).Trim()),
                ]);
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

    private sealed record NetChainResult(
        ProcessResult BuildResult,
        IReadOnlyList<Diagnostic> CompilerDiagnostics,
        IReadOnlyList<Diagnostic> SarifDiagnostics,
        ProcessResult FormatResult,
        IReadOnlyList<Diagnostic> FormatDiagnostics,
        bool SchemaCheckSucceeded,
        IReadOnlyList<Diagnostic> SchemaDiagnostics,
        ProcessResult EfMigrationsResult,
        IReadOnlyList<Diagnostic> EfMigrationsDiagnostics);

    private void CleanStaleArtifacts()
    {
        // Only the current run's diagnostics should show up in the report.
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
