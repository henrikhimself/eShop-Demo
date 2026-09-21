// <copyright file="ToolExecutor.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Output;

namespace Hj.EShop.Cli.Execution;

internal sealed class ToolExecutor(
    IGlobalOptionsAccessor globalOptions,
    ILocalToolLocator localToolLocator,
    IContainerRunner containerRunner,
    IProcessRunner processRunner) : IToolExecutor
{
    public Task<ProcessResult> RunAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        ExecutionMode mode = invocation.ForceMode ?? globalOptions.Options.Tools;

        // AiOutputSink is documented as plain, ANSI-free text, so a subprocess's own
        // raw ANSI color codes would be pure noise there. Human mode keeps them.
        if (globalOptions.Options.Output == OutputMode.Ai)
        {
            Dictionary<string, string> environmentVariables = invocation.EnvironmentVariables is null
                ? []
                : new Dictionary<string, string>(invocation.EnvironmentVariables);
            environmentVariables["NO_COLOR"] = "1";
            invocation = invocation with { EnvironmentVariables = environmentVariables };
        }

        return mode switch
        {
            ExecutionMode.Local => RunLocalAsync(invocation, cancellationToken),
            ExecutionMode.Container => containerRunner.RunAsync(invocation, cancellationToken),
            ExecutionMode.Auto => IsUsableLocally(invocation.Tool)
                ? RunLocalAsync(invocation, cancellationToken)
                : containerRunner.RunAsync(invocation, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(invocation), mode, message: null),
        };
    }

    // On PATH is necessary but not sufficient: a tool whose version PrerequisiteChecker
    // already flagged as mismatched (e.g. Node 22 on PATH when .nvmrc pins 24) must not
    // be treated as usable under --tools auto - route those to the container instead of
    // silently running with the wrong version.
    private bool IsUsableLocally(string tool)
    {
        return localToolLocator.IsOnPath(tool) && !globalOptions.Options.UnusableLocalTools.Contains(tool);
    }

    private Task<ProcessResult> RunLocalAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        return processRunner.RunAsync(
            new ProcessRequest(
                invocation.Tool, invocation.Arguments, invocation.WorkingDirectory,
                invocation.EnvironmentVariables, Interactive: invocation.Interactive),
            cancellationToken);
    }
}
