// <copyright file="UnitTestCommand.cs" company="Henrik Jensen">
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
using Spectre.Console.Cli;

namespace Hj.EShop.Cli.Commands.Test;

internal sealed class UnitTestCommand(IOutputSink output, IToolExecutor toolExecutor, RepoPaths paths) : AsyncCommand<TestSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, TestSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "Test Result");

        TestResultsDirectory.ClearStale(paths);

        // Independent - the frontend's Vitest run touches none of the files the
        // backend's dotnet test/mdreport chain does, and produces no .trx of its own.
        string relativeSolution = Path.GetRelativePath(paths.Root, paths.Solution);
        string relativeWebDir = Path.GetRelativePath(paths.Root, paths.SellerPortalWebDir);
        Task<BackendResult> backendTask = RunBackendAsync(relativeSolution, cancellationToken);
        Task<ProcessResult> frontendTask = RunFrontendAsync(relativeWebDir, cancellationToken);
        await Task.WhenAll(backendTask, frontendTask);

        // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
        await output.FlushPendingStepsAsync();

        BackendResult backend = backendTask.Result;
        ProcessResult frontendTest = frontendTask.Result;

        output.Heading(1, "Backend Test Result");
        output.Code("plain", backend.Test.StandardOutput + backend.Test.StandardError);
        output.Text(backend.Report.StandardOutput);

        output.Heading(1, "Frontend Test Result");
        output.Code("plain", frontendTest.StandardOutput + frontendTest.StandardError);

        return backend.Test.Succeeded && frontendTest.Succeeded ? 0 : 1;
    }

    private async Task<BackendResult> RunBackendAsync(string relativeSolution, CancellationToken cancellationToken)
    {
        // See doc/CHRONICLE.md - container tool arguments must stay repo-relative.
        string relativeTestResultsDir = Path.GetRelativePath(paths.Root, paths.TestResultsDir);

        ProcessResult test;
        using (output.BeginStep("dotnet test"))
        {
            test = await toolExecutor.RunAsync(
                new ToolInvocation(
                    "dotnet",
                    [
                        "test", "--solution", relativeSolution, "--report-trx", "--hangdump", "--hangdump-timeout", "30s",
                        "--results-directory", relativeTestResultsDir,

                        // See doc/CHRONICLE.md - Integration-trait tests are opt-in only.
                        "--filter-not-trait", "Category=Integration",
                    ],
                    WorkingDirectory: paths.Root),
                cancellationToken);
        }

        ProcessResult report;
        using (output.BeginStep("dotnet mdreport trx"))
        {
            report = await toolExecutor.RunAsync(
                new ToolInvocation("dotnet", ["mdreport", "trx", "**/*.trx"], WorkingDirectory: paths.Root),
                cancellationToken);
        }

        return new BackendResult(test, report);
    }

    private async Task<ProcessResult> RunFrontendAsync(string relativeWebDir, CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep("pnpm test");
        return await toolExecutor.RunAsync(
            new ToolInvocation("pnpm", ["--dir", relativeWebDir, "run", "test", "--run"], WorkingDirectory: paths.Root),
            cancellationToken);
    }

    private sealed record BackendResult(ProcessResult Test, ProcessResult Report);
}
