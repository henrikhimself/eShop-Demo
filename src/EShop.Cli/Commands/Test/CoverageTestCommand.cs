// <copyright file="CoverageTestCommand.cs" company="Henrik Jensen">
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

// Runs `eshop test coverage`. Report-only: pass/fail comes from the test run,
// not a coverage threshold.
internal sealed class CoverageTestCommand(IOutputSink output, IToolExecutor toolExecutor, RepoPaths paths) : AsyncCommand<TestSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, TestSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "Coverage Result");

        TestResultsDirectory.ClearStale(paths);

        string relativeSolution = Path.GetRelativePath(paths.Root, paths.Solution);
        string coberturaPath = Path.Combine(paths.TmpDir, "coverage.cobertura.xml");
        string coverageDir = Path.Combine(paths.TmpDir, "coverage");

        // See doc/CHRONICLE.md - container tool arguments must stay repo-relative
        // (the absolute variants are still used below to read the results back).
        string relativeCoberturaPath = Path.GetRelativePath(paths.Root, coberturaPath);
        string relativeCoverageDir = Path.GetRelativePath(paths.Root, coverageDir);
        string relativeTestResultsDir = Path.GetRelativePath(paths.Root, paths.TestResultsDir);

        ProcessResult test;
        using (output.BeginStep("dotnet-coverage collect"))
        {
            test = await toolExecutor.RunAsync(
                new ToolInvocation(
                    "dotnet",
                    [
                        "dotnet-coverage", "collect", "-o", relativeCoberturaPath, "-f", "cobertura", "--",
                        "dotnet", "test", "--solution", relativeSolution, "--report-trx", "--hangdump", "--hangdump-timeout", "30s",
                        "--results-directory", relativeTestResultsDir,
                    ],
                    WorkingDirectory: paths.Root),
                cancellationToken);
        }

        using (output.BeginStep("reportgenerator"))
        {
            await toolExecutor.RunAsync(
                new ToolInvocation(
                    "dotnet",
                    [
                        "reportgenerator", $"-reports:{relativeCoberturaPath}", $"-targetdir:{relativeCoverageDir}",
                        "-reporttypes:MarkdownSummaryGithub;Html", "-assemblyfilters:-*.Tests",
                    ],
                    WorkingDirectory: paths.Root),
                cancellationToken);
        }

        // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
        await output.FlushPendingStepsAsync();

        output.Code("plain", test.StandardOutput + test.StandardError);

        string summaryPath = Path.Combine(coverageDir, "SummaryGithub.md");
        if (File.Exists(summaryPath))
        {
            // A file:// URL, not the report content - copy/pasteable straight into a
            // browser's address bar, instead of dumping the whole report to the terminal.
            string htmlReportUrl = new Uri(Path.Combine(coverageDir, "index.html")).AbsoluteUri;
            output.Text($"Coverage report: {htmlReportUrl}");
        }
        else
        {
            output.Text("No coverage report was produced.");
        }

        return test.Succeeded ? 0 : 1;
    }
}
