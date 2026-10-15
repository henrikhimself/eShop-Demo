// <copyright file="AppHostSessionRunner.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;

namespace Hj.EShop.Cli.AppHost;

internal sealed class AppHostSessionRunner(
    IOutputSink output,
    IToolExecutor toolExecutor,
    IConsoleKeyReader keyReader,
    IBuildRunner buildRunner,
    RepoPaths paths,
    IRunSettingsReader runSettingsReader) : IAppHostSessionRunner
{
    public async Task<int> RunAsync(IReadOnlyList<string> extraArguments, CancellationToken cancellationToken)
    {
        string relativeAppHost = Path.GetRelativePath(paths.Root, paths.AppHostProject);
        IReadOnlyDictionary<string, string> runEnvironmentVariables = await runSettingsReader.GetRunEnvironmentVariablesAsync(cancellationToken);

        while (true)
        {
            ProcessResult startResult = await StartAsync(relativeAppHost, extraArguments, runEnvironmentVariables, cancellationToken);
            if (!startResult.Succeeded)
            {
                return startResult.ExitCode;
            }

            AppHostAction action;
            try
            {
                output.Text("Press 'b' to rebuild and restart, or 'q' to stop the AppHost and exit.");
                action = await WaitForActionAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                action = AppHostAction.Quit;
            }

            ProcessResult stopResult = await StopAsync(relativeAppHost, runEnvironmentVariables);
            if (!stopResult.Succeeded || action == AppHostAction.Quit)
            {
                return stopResult.ExitCode;
            }

            try
            {
                int buildExitCode = await buildRunner.RunAsync(cancellationToken);
                if (buildExitCode != 0)
                {
                    return buildExitCode;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return stopResult.ExitCode;
            }
        }
    }

    private async Task<ProcessResult> StartAsync(
        string relativeAppHost,
        IReadOnlyList<string> extraArguments,
        IReadOnlyDictionary<string, string> runEnvironmentVariables,
        CancellationToken cancellationToken)
    {
        ProcessResult startResult = await toolExecutor.RunAsync(
            new ToolInvocation(
                "aspire",
                ["start", "--no-build", "--non-interactive", "--apphost", relativeAppHost, .. extraArguments],
                WorkingDirectory: paths.Root,
                EnvironmentVariables: runEnvironmentVariables),
            cancellationToken);

        // Not streamed live (this ToolInvocation isn't Interactive) - `aspire start`'s
        // own captured output is where the dashboard URL banner lives, so it has to be
        // printed explicitly or the user never sees it.
        output.Text(startResult.StandardOutput + startResult.StandardError);
        return startResult;
    }

    private Task<ProcessResult> StopAsync(string relativeAppHost, IReadOnlyDictionary<string, string> runEnvironmentVariables)
    {
        return toolExecutor.RunAsync(
            new ToolInvocation(
                "aspire", ["stop", "--non-interactive", "--apphost", relativeAppHost],
                WorkingDirectory: paths.Root, EnvironmentVariables: runEnvironmentVariables),
            CancellationToken.None);
    }

    private async Task<AppHostAction> WaitForActionAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            char? key = await keyReader.ReadKeyAsync(cancellationToken);
            if (key is 'q' or 'Q')
            {
                return AppHostAction.Quit;
            }

            if (key is 'b' or 'B')
            {
                return AppHostAction.Rebuild;
            }

            if (key is null)
            {
                // Stdin is at EOF (e.g. redirected from /dev/null, or already closed -
                // as happens when `eshop run --agent` is invoked non-interactively).
                // This is not a quit request: keep the AppHost running and block until
                // a real exit signal (Ctrl+C/SIGTERM, surfaced as cancellation) arrives.
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
    }

    private enum AppHostAction
    {
        Quit,
        Rebuild,
    }
}
