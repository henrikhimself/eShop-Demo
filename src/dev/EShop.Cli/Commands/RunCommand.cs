// <copyright file="RunCommand.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Commands;

internal sealed class RunCommand(
    IOutputSink output,
    IAppHostGuard guard,
    ILocalToolLocator localToolLocator,
    IAppHostSessionRunner sessionRunner,
    IContainerRunner containerRunner,
    IGlobalOptionsAccessor globalOptions,
    RepoPaths paths,
    IRunSettingsReader runSettingsReader) : AsyncCommand<RunSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, RunSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "Run AppHost");

        if (await guard.IsAlreadyRunningAsync(cancellationToken))
        {
            output.Status(Severity.Failure, "An aspire session for this AppHost is already running. Stop it and then re-run this command.");
            return 1;
        }

        if (localToolLocator.IsOnPath("aspire"))
        {
            return await sessionRunner.RunAsync(settings.AppHostArguments, cancellationToken);
        }

        // No local `aspire` CLI - the whole start/wait/stop cycle must run as one
        // continuous session inside a single container (see AppHostContainerScript).
        string relativeAppHost = Path.GetRelativePath(paths.Root, paths.AppHostProject);
        string relativeCliBinary = Path.GetRelativePath(paths.Root, Environment.ProcessPath
            ?? throw new InvalidOperationException("The CLI executable path is unavailable."));
        string[] buildGlobalArguments = BuildGlobalArguments(globalOptions.Options.Tools);
        string[] runGlobalArguments = BuildGlobalArguments(ExecutionMode.Local);
        string script = AppHostContainerScript.Build(
            relativeAppHost, settings.AppHostArguments, relativeCliBinary, buildGlobalArguments, runGlobalArguments);
        IReadOnlyDictionary<string, string> runEnvironmentVariables = await runSettingsReader.GetRunEnvironmentVariablesAsync(cancellationToken);
        ProcessResult result = await containerRunner.RunAsync(
            new ToolInvocation("bash", ["-c", script], Interactive: true, EnvironmentVariables: runEnvironmentVariables), cancellationToken);
        return result.ExitCode;
    }

    private string[] BuildGlobalArguments(ExecutionMode tools)
    {
        List<string> arguments = ["--tools", tools.ToString()];
        if (globalOptions.Options.Output == OutputMode.Ai)
        {
            arguments.Add("--agent");
        }

        if (globalOptions.Options.Debug)
        {
            arguments.Add("--debug");
        }

        return [.. arguments];
    }
}
