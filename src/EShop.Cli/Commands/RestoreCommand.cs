// <copyright file="RestoreCommand.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Commands;

// Restores the solution and, on a NuGet restore failure, searches the captured
// binlog for errors and warnings so the failure is actionable.
internal sealed class RestoreCommand(IOutputSink output, IToolExecutor toolExecutor, RepoPaths paths)
    : AsyncCommand<DefaultSettings>
{
    private const string BinlogFileName = "restore.binlog";

    // See doc/CHRONICLE.md - container tool arguments must stay repo-relative.
    private static string RelativeBinlogPath => Path.Combine("tmp", BinlogFileName);

    protected override async Task<int> ExecuteAsync(CommandContext context, DefaultSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "Restore Result");

        // Independent - NuGet package restore, the tool manifest restore, and the
        // frontend's own package manager touch disjoint state.
        string relativeSolution = Path.GetRelativePath(paths.Root, paths.Solution);
        string relativeWebDir = Path.GetRelativePath(paths.Root, paths.SellerPortalWebDir);
        Task<ProcessResult> restoreTask = RunRestoreAsync(relativeSolution, cancellationToken);
        Task<ProcessResult> toolRestoreTask = RunToolRestoreAsync(cancellationToken);
        Task<ProcessResult> pnpmInstallTask = RunPnpmInstallAsync(relativeWebDir, cancellationToken);
        await Task.WhenAll(restoreTask, toolRestoreTask, pnpmInstallTask);

        // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
        await output.FlushPendingStepsAsync();

        ProcessResult restore = restoreTask.Result;
        ProcessResult toolRestore = toolRestoreTask.Result;
        ProcessResult pnpmInstall = pnpmInstallTask.Result;

        if (restore.Succeeded)
        {
            output.Text("Restoring successfully completed.");

            // Only scratch output kept for a failed run's diagnostics - a successful
            // run has no further use for it.
            string binlogPath = Path.Combine(paths.TmpDir, BinlogFileName);
            if (File.Exists(binlogPath))
            {
                File.Delete(binlogPath);
            }
        }
        else
        {
            output.Text("Restoring FAILED to complete.");
            await ReportBinlogAsync(cancellationToken);
        }

        output.Text(toolRestore.Succeeded ? "Dotnet tools successfully restored." : "Dotnet tool restore FAILED.");
        output.Text(pnpmInstall.Succeeded ? "Frontend dependencies successfully installed." : "Frontend dependency install FAILED.");

        return restore.Succeeded && toolRestore.Succeeded && pnpmInstall.Succeeded ? 0 : 1;
    }

    private async Task<ProcessResult> RunRestoreAsync(string relativeSolution, CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep("dotnet restore");
        return await toolExecutor.RunAsync(
            new ToolInvocation(
                "dotnet",
                ["restore", relativeSolution, "--verbosity", "detailed", $"-bl:{RelativeBinlogPath}"],
                WorkingDirectory: paths.Root),
            cancellationToken);
    }

    private async Task<ProcessResult> RunToolRestoreAsync(CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep("dotnet tool restore");
        return await toolExecutor.RunAsync(
            new ToolInvocation("dotnet", ["tool", "restore"], WorkingDirectory: paths.Root),
            cancellationToken);
    }

    private async Task<ProcessResult> RunPnpmInstallAsync(string relativeWebDir, CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep("pnpm install");
        return await toolExecutor.RunAsync(
            new ToolInvocation("pnpm", ["--dir", relativeWebDir, "install", "--frozen-lockfile"], WorkingDirectory: paths.Root),
            cancellationToken);
    }

    private async Task ReportBinlogAsync(CancellationToken cancellationToken)
    {
        ProcessResult errors;
        using (output.BeginStep("search binlog for errors"))
        {
            errors = await toolExecutor.RunAsync(
                new ToolInvocation("dotnet", ["binlogtool", "search", RelativeBinlogPath, "$error"], WorkingDirectory: paths.Root),
                cancellationToken);
        }

        ProcessResult warnings;
        using (output.BeginStep("search binlog for warnings"))
        {
            warnings = await toolExecutor.RunAsync(
                new ToolInvocation("dotnet", ["binlogtool", "search", RelativeBinlogPath, "$warning"], WorkingDirectory: paths.Root),
                cancellationToken);
        }

        await output.FlushPendingStepsAsync();

        output.Heading(2, "Binlog Errors");
        output.Code("plain", errors.StandardOutput);

        output.Heading(2, "Binlog Warnings");
        output.Code("plain", warnings.StandardOutput);
    }
}
