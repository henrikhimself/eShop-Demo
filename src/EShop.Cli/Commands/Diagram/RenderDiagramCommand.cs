// <copyright file="RenderDiagramCommand.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Commands.Diagram;

// Renders into a scratch directory first and only moves the result into place once
// rendering succeeds, so a failed run can't overwrite a good diagram with a broken one.
internal sealed class RenderDiagramCommand(IOutputSink output, IToolExecutor toolExecutor, RepoPaths paths)
    : AsyncCommand<RenderDiagramSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RenderDiagramSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "Render PlantUML Result");

        string pumlPath = Path.GetFullPath(settings.File);
        if (!pumlPath.EndsWith(".puml", StringComparison.Ordinal))
        {
            output.Status(Severity.Failure, $"'{settings.File}' is not a .puml file.");
            return 1;
        }

        if (!File.Exists(pumlPath))
        {
            output.Status(Severity.Failure, $"'{settings.File}' does not exist.");
            return 1;
        }

        string outputPath = Path.ChangeExtension(pumlPath, ".svg");
        string scratchDir = Path.Combine(paths.TmpDir, $"render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scratchDir);

        try
        {
            string scratchFile = Path.Combine(scratchDir, Path.GetFileName(pumlPath));
            File.Copy(pumlPath, scratchFile);

            // See doc/CHRONICLE.md - container tool arguments must stay repo-relative.
            string relativeScratchFile = Path.GetRelativePath(paths.Root, scratchFile);
            ToolInvocation invocation = new("plantuml", ["-tsvg", relativeScratchFile], WorkingDirectory: paths.Root);
            ProcessResult result;
            using (output.BeginStep($"render {settings.File}"))
            {
                result = await toolExecutor.RunAsync(invocation, cancellationToken);
            }

            // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
            await output.FlushPendingStepsAsync();

            string scratchOutput = Path.ChangeExtension(scratchFile, ".svg");
            if (!result.Succeeded || !File.Exists(scratchOutput))
            {
                output.Status(Severity.Failure, $"Could not render '{settings.File}'.");
                output.Code("plain", result.StandardOutput + result.StandardError);
                return 1;
            }

            File.Move(scratchOutput, outputPath, overwrite: true);
            output.Status(Severity.Success, $"Rendered '{settings.File}' to '{outputPath}'.");
            return 0;
        }
        finally
        {
            Directory.Delete(scratchDir, recursive: true);
        }
    }
}
