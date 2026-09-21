// <copyright file="GlobalOptionsInterceptor.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Prerequisites;
using Spectre.Console.Cli;

namespace Hj.EShop.Cli;

// Runs once per invocation, after Spectre binds --tools/--agent but before any
// command constructor resolves - this is the one and only place either global option
// gets parsed. Precedence: explicit flag > environment variable > default. Also runs
// the dev-cert trust and local-prerequisite checks before every command.
internal sealed class GlobalOptionsInterceptor(
    GlobalOptionsAccessor accessor,
    IDevCertificateInstaller devCertificateInstaller,
    IPrerequisiteChecker prerequisiteChecker) : ICommandInterceptor
{
    private const string ToolsEnvironmentVariable = "ESHOP_TOOLS";
    private const string AgentEnvironmentVariable = "ESHOP_AGENT";

    public void Intercept(CommandContext context, CommandSettings settings)
    {
        if (settings is not GlobalSettings globalSettings)
        {
            return;
        }

        ExecutionMode tools = globalSettings.Tools ?? ResolveToolsFromEnvironment() ?? ExecutionMode.Auto;
        OutputMode output = ResolveOutput(globalSettings.Agent);

        // Both .GetAwaiter().GetResult() calls here are safe: Intercept has no async
        // overload in this pinned Spectre.Console.Cli version, and a console app's
        // top-level Main has no SynchronizationContext to deadlock against. The check
        // must run before accessor.Resolve(...) below, since ToolExecutor's --tools
        // auto routing needs its UnusableLocalTools result baked into GlobalOptions.
        PrerequisiteCheckResult prerequisiteCheckResult =
            prerequisiteChecker.CheckAsync(CancellationToken.None).GetAwaiter().GetResult();
        accessor.Resolve(new GlobalOptions(tools, output, prerequisiteCheckResult.UnusableLocalTools));

        // IOutputSink can't be constructor-injected here because it depends on the
        // accessor.Resolve(...) call above, so this builds a throwaway instance directly
        // from the mode just resolved instead.
        IOutputSink outputSink = OutputSinkFactory.Create(output);

        IReadOnlyList<string> issuesToShow = IssuesToShow(prerequisiteCheckResult, tools);
        if (issuesToShow.Count > 0)
        {
            outputSink.Heading(1, "Local Prerequisites");
            foreach (string issue in issuesToShow)
            {
                outputSink.Text(issue);
            }
        }

        devCertificateInstaller.EnsureTrustedAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public void InterceptResult(CommandContext context, CommandSettings settings, ref int result)
    {
    }

    // GeneralIssues are always worth showing. LocalToolIssues are only shown under an
    // explicit --tools local: --tools auto (the default) already routes a flagged tool
    // to the container instead of using it, so the warning would just be noise about a
    // problem the CLI already worked around; --tools container never touches local
    // tools at all. --tools local bypasses that routing and uses the tool anyway, so
    // the user needs to know it's about to fail.
    internal static IReadOnlyList<string> IssuesToShow(PrerequisiteCheckResult result, ExecutionMode tools)
    {
        return tools == ExecutionMode.Local
            ? [.. result.GeneralIssues, .. result.LocalToolIssues]
            : result.GeneralIssues;
    }

    private static ExecutionMode? ResolveToolsFromEnvironment()
    {
        string? value = Environment.GetEnvironmentVariable(ToolsEnvironmentVariable);

        return !string.IsNullOrEmpty(value) && Enum.TryParse<ExecutionMode>(value, ignoreCase: true, out ExecutionMode parsed)
            ? parsed
            : null;
    }

    private static OutputMode ResolveOutput(bool agentFlag)
    {
        if (agentFlag)
        {
            return OutputMode.Ai;
        }

        string? environmentValue = Environment.GetEnvironmentVariable(AgentEnvironmentVariable);
        if (!string.IsNullOrEmpty(environmentValue))
        {
            return IsTruthy(environmentValue) ? OutputMode.Ai : OutputMode.Human;
        }

        return Console.IsOutputRedirected ? OutputMode.Ai : OutputMode.Human;
    }

    private static bool IsTruthy(string value)
    {
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
