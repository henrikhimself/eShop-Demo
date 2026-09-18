// <copyright file="E2ETestCommand.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;
using Spectre.Console.Cli;

namespace Hj.EShop.Cli.Commands.Test;

// Runs `eshop test e2e`. It always runs dotnet restore/test in the container,
// which has the Playwright/Chromium install this suite needs (see
// doc/adr/0011-playwright-for-browser-e2e-tests.md).
internal sealed class E2ETestCommand(IOutputSink output, IToolExecutor toolExecutor, IAppHostGuard guard, RepoPaths paths)
    : AsyncCommand<E2ETestSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, E2ETestSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "E2E Test Result");

        if (!string.IsNullOrWhiteSpace(settings.FilterClass) && !string.IsNullOrWhiteSpace(settings.FilterMethod))
        {
            output.Status(Severity.Failure, "Use either --filter-class or --filter-method, not both.");
            return 1;
        }

        if (await guard.IsAlreadyRunningAsync(cancellationToken))
        {
            output.Status(Severity.Failure, "An aspire session for this AppHost is already running. Stop it and then re-run this command.");
            return 1;
        }

        TestResultsDirectory.ClearStale(paths);

        string relativeAppHost = Path.GetRelativePath(paths.Root, paths.AppHostProject);
        using (output.BeginStep("dotnet restore (AppHost)"))
        {
            await toolExecutor.RunAsync(
                new ToolInvocation("dotnet", ["restore", relativeAppHost], WorkingDirectory: paths.Root, ForceMode: ExecutionMode.Container),
                cancellationToken);
        }

        string relativeE2EProject = Path.GetRelativePath(paths.Root, paths.E2ETestProject);

        // See doc/CHRONICLE.md - container tool arguments must stay repo-relative.
        string relativeTestResultsDir = Path.GetRelativePath(paths.Root, paths.TestResultsDir);
        List<string> testArguments =
        [
            "test", relativeE2EProject, "--report-trx", "--hangdump", "--hangdump-timeout", "15m",
            "--results-directory", relativeTestResultsDir,
        ];
        if (!string.IsNullOrWhiteSpace(settings.FilterClass))
        {
            testArguments.AddRange(["--filter-class", settings.FilterClass]);
        }
        else if (!string.IsNullOrWhiteSpace(settings.FilterMethod))
        {
            testArguments.AddRange(["--filter-method", settings.FilterMethod]);
        }

        ProcessResult test;
        using (output.BeginStep("dotnet test (e2e)"))
        {
            test = await toolExecutor.RunAsync(
                new ToolInvocation(
                    "dotnet",
                    testArguments,
                    WorkingDirectory: paths.Root,
                    ForceMode: ExecutionMode.Container),
                cancellationToken);
        }

        ProcessResult report;
        using (output.BeginStep("dotnet mdreport trx"))
        {
            report = await toolExecutor.RunAsync(
                new ToolInvocation("dotnet", ["mdreport", "trx", "**/*.trx"], WorkingDirectory: paths.Root),
                cancellationToken);
        }

        // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
        await output.FlushPendingStepsAsync();

        output.Code("plain", test.StandardOutput + test.StandardError);
        output.Text(report.StandardOutput);

        return test.Succeeded ? 0 : 1;
    }
}
