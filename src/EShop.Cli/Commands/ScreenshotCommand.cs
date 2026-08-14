// <copyright file="ScreenshotCommand.cs" company="Henrik Jensen">
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

using System.Globalization;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;
using Spectre.Console.Cli;

namespace Hj.EShop.Cli.Commands;

// See doc/CHRONICLE.md - always runs in the container, which has the real Playwright
// Chromium install (and its shared library dependencies) this needs.
internal sealed class ScreenshotCommand(IOutputSink output, IToolExecutor toolExecutor, RepoPaths paths)
    : AsyncCommand<ScreenshotSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, ScreenshotSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "Screenshot Result");

        string absoluteOutputPath = Path.IsPathRooted(settings.OutputPath)
            ? settings.OutputPath
            : Path.Combine(paths.Root, settings.OutputPath);
        string relativeOutputPath = Path.GetRelativePath(paths.Root, absoluteOutputPath);

        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath)!);

        Directory.CreateDirectory(paths.TmpDir);
        string scriptPath = Path.Combine(paths.TmpDir, ScreenshotContainerScript.FileName);
        await File.WriteAllTextAsync(scriptPath, ScreenshotContainerScript.Script, cancellationToken);
        string relativeScriptPath = Path.GetRelativePath(paths.Root, scriptPath);

        string relativeProfileDir = Path.GetRelativePath(paths.Root, Path.Combine(paths.TmpDir, "screenshot-profile"));

        List<string> arguments =
        [
            relativeScriptPath,
            "--url", settings.Url,
            "--output", relativeOutputPath,
            "--width", settings.Width.ToString(CultureInfo.InvariantCulture),
            "--height", settings.Height.ToString(CultureInfo.InvariantCulture),
            "--profile-dir", relativeProfileDir,
        ];

        if (settings.Login)
        {
            arguments.Add("--login");
            arguments.AddRange(["--username", settings.Username, "--password", settings.Password]);
        }

        ProcessResult result;
        using (output.BeginStep($"screenshot {settings.Url}"))
        {
            result = await toolExecutor.RunAsync(
                new ToolInvocation("node", arguments, WorkingDirectory: paths.Root, ForceMode: ExecutionMode.Container),
                cancellationToken);
        }

        // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
        await output.FlushPendingStepsAsync();

        if (!result.Succeeded || !File.Exists(absoluteOutputPath))
        {
            output.Status(Severity.Failure, $"Could not screenshot '{settings.Url}'.");
            output.Code("plain", result.StandardOutput + result.StandardError);
            return 1;
        }

        output.Status(Severity.Success, $"Screenshot saved to '{absoluteOutputPath}'.");
        return 0;
    }
}
