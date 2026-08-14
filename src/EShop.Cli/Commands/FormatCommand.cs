// <copyright file="FormatCommand.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Commands.Generate;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;
using Spectre.Console.Cli;

namespace Hj.EShop.Cli.Commands;

// MUTATES the working tree: applies every auto-fixable dotnet format, rumdl, and
// ESLint fix, then regenerates the API schema. Always succeeds - any remaining
// issue still shows up in `eshop build`'s report for manual follow-up.
internal sealed class FormatCommand(IOutputSink output, IToolExecutor toolExecutor, IApiSchemaGenerator apiSchemaGenerator, RepoPaths paths)
    : AsyncCommand<DefaultSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DefaultSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "Format Result");

        // dotnet format's 3 passes + the schema regen all touch the same solution's
        // .cs files/obj state and must stay one branch; rumdl only touches markdown
        // and pnpm lint --fix only touches the frontend's TS/JS - independent of that
        // branch and of each other.
        string relativeSolution = Path.GetRelativePath(paths.Root, paths.Solution);
        string relativeWebDir = Path.GetRelativePath(paths.Root, paths.SellerPortalWebDir);
        Task<(IReadOnlyList<FormatStepResult> Steps, ApiSchemaGenerationResult Schema)> dotnetChainTask =
            RunDotnetFormatChainAsync(relativeSolution, cancellationToken);
        Task<FormatStepResult> markdownTask = RunAndCaptureAsync(
            "Markdown", new ToolInvocation("rumdl", ["fmt", "."], WorkingDirectory: paths.Root), cancellationToken);
        Task<FormatStepResult> frontendTask = RunAndCaptureAsync(
            "Frontend",
            new ToolInvocation("pnpm", ["--dir", relativeWebDir, "run", "lint", "--fix"], WorkingDirectory: paths.Root),
            cancellationToken);
        await Task.WhenAll(dotnetChainTask, markdownTask, frontendTask);

        // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
        await output.FlushPendingStepsAsync();

        (IReadOnlyList<FormatStepResult> dotnetSteps, ApiSchemaGenerationResult schemaResult) = dotnetChainTask.Result;

        foreach (FormatStepResult step in dotnetSteps)
        {
            Report(step);
        }

        Report(markdownTask.Result);
        Report(frontendTask.Result);

        output.Heading(2, "Generated API types");
        output.Code("plain", schemaResult.Succeeded ? schemaResult.Message : schemaResult.Message + "\n" + schemaResult.Details);

        return 0;
    }

    private async Task<(IReadOnlyList<FormatStepResult> Steps, ApiSchemaGenerationResult Schema)> RunDotnetFormatChainAsync(
        string relativeSolution, CancellationToken cancellationToken)
    {
        FormatStepResult whitespace = await RunAndCaptureAsync(
            "Whitespace",
            new ToolInvocation("dotnet", ["format", "whitespace", relativeSolution, "--verbosity", "normal"], WorkingDirectory: paths.Root),
            cancellationToken);
        FormatStepResult style = await RunAndCaptureAsync(
            "Style",
            new ToolInvocation("dotnet", ["format", "style", relativeSolution, "--verbosity", "normal"], WorkingDirectory: paths.Root),
            cancellationToken);
        FormatStepResult analyzers = await RunAndCaptureAsync(
            "Analyzers",
            new ToolInvocation("dotnet", ["format", "analyzers", relativeSolution, "--verbosity", "normal"], WorkingDirectory: paths.Root),
            cancellationToken);

        ApiSchemaGenerationResult schemaResult;
        using (output.BeginStep("Generate API types"))
        {
            schemaResult = await apiSchemaGenerator.GenerateAsync(cancellationToken);
        }

        return ([whitespace, style, analyzers], schemaResult);
    }

    private async Task<FormatStepResult> RunAndCaptureAsync(string heading, ToolInvocation invocation, CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep(heading);
        ProcessResult result = await toolExecutor.RunAsync(invocation, cancellationToken);
        return new FormatStepResult(heading, result.StandardOutput + result.StandardError);
    }

    private void Report(FormatStepResult step)
    {
        output.Heading(2, step.Heading);
        output.Code("plain", step.Content);
    }

    private sealed record FormatStepResult(string Heading, string Content);
}
