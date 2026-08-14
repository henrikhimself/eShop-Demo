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

using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;

namespace Hj.EShop.Cli.AppHost;

internal sealed class AppHostSessionRunner(IOutputSink output, IToolExecutor toolExecutor, IConsoleKeyReader keyReader, RepoPaths paths)
    : IAppHostSessionRunner
{
    public async Task<int> RunAsync(IReadOnlyList<string> extraArguments, CancellationToken cancellationToken)
    {
        string relativeAppHost = Path.GetRelativePath(paths.Root, paths.AppHostProject);

        ProcessResult startResult = await toolExecutor.RunAsync(
            new ToolInvocation(
                "aspire",
                ["start", "--no-build", "--non-interactive", "--apphost", relativeAppHost, .. extraArguments],
                WorkingDirectory: paths.Root),
            cancellationToken);

        // Not streamed live (this ToolInvocation isn't Interactive) - `aspire start`'s
        // own captured output is where the dashboard URL banner lives, so it has to be
        // printed explicitly or the user never sees it.
        output.Text(startResult.StandardOutput + startResult.StandardError);

        if (!startResult.Succeeded)
        {
            // Nothing started - there is nothing to wait for 'q' on or to stop.
            return startResult.ExitCode;
        }

        output.Text("Press 'q' to stop the AppHost and exit.");

        try
        {
            await WaitForQKeyAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Falls through to the same single stop call the 'q'/EOF path takes below.
        }

        ProcessResult stopResult = await toolExecutor.RunAsync(
            new ToolInvocation("aspire", ["stop", "--non-interactive", "--apphost", relativeAppHost], WorkingDirectory: paths.Root),
            CancellationToken.None);

        return stopResult.ExitCode;
    }

    private async Task WaitForQKeyAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            char? key = await keyReader.ReadKeyAsync(cancellationToken);
            if (key is 'q' or 'Q')
            {
                return;
            }

            if (key is null)
            {
                // Stdin is at EOF (e.g. redirected from /dev/null, or already closed -
                // as happens when `eshop run --agent` is invoked non-interactively).
                // This is not a quit request: keep the AppHost running and block until
                // a real exit signal (Ctrl+C/SIGTERM, surfaced as cancellation) arrives.
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return;
            }
        }
    }
}
