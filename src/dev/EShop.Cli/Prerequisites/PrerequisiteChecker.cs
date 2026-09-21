// <copyright file="PrerequisiteChecker.cs" company="Henrik Jensen">
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

using System.Runtime.InteropServices;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;

namespace Hj.EShop.Cli.Prerequisites;

// Checks the local prerequisites every command reports: host architecture and
// local dotnet, Node.js, and pnpm availability against the repo's pins.
internal sealed class PrerequisiteChecker(
    ILocalToolLocator localToolLocator,
    IProcessRunner processRunner,
    IPinnedVersionReader pinnedVersionReader,
    RepoPaths paths) : IPrerequisiteChecker
{
    public async Task<PrerequisiteCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        List<string> generalIssues = [];
        List<string> localToolIssues = [];
        HashSet<string> unusableTools = [];

        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            generalIssues.Add(
                $"host architecture: {RuntimeInformation.ProcessArchitecture} - `eshop run` and "
                + "`eshop test e2e` need the SQL Server container image, which is amd64-only. See "
                + "doc/TODO.md for the current state of arm64 workarounds.");
        }

        if (!await IsDotnetUsableAsync(cancellationToken))
        {
            localToolIssues.Add(
                "dotnet: not usable here - missing, or no installed SDK satisfies global.json's pin. "
                + "Install the .NET SDK version global.json pins.");
            unusableTools.Add("dotnet");
        }

        IReadOnlyList<string> nodeIssues = await CheckNodeAsync(cancellationToken);
        if (nodeIssues.Count > 0)
        {
            localToolIssues.AddRange(nodeIssues);
            unusableTools.Add("node");
        }

        IReadOnlyList<string> pnpmIssues = await CheckPnpmAsync(cancellationToken);
        if (pnpmIssues.Count > 0)
        {
            localToolIssues.AddRange(pnpmIssues);
            unusableTools.Add("pnpm");
        }

        return new PrerequisiteCheckResult(generalIssues, localToolIssues, unusableTools);
    }

    private async Task<bool> IsDotnetUsableAsync(CancellationToken cancellationToken)
    {
        // Relies on global.json's own rollForward resolution as the correctness check
        // (a real `dotnet` invocation hits the exact same failure) rather than
        // reimplementing SDK version comparison here.
        if (!localToolLocator.IsOnPath("dotnet"))
        {
            return false;
        }

        ProcessResult result = await processRunner.RunAsync(
            new ProcessRequest("dotnet", ["--version"], paths.Root),
            cancellationToken);
        return result.Succeeded;
    }

    private async Task<IReadOnlyList<string>> CheckNodeAsync(CancellationToken cancellationToken)
    {
        string pinned = await pinnedVersionReader.GetPinnedNodeVersionAsync(cancellationToken);

        if (!localToolLocator.IsOnPath("node"))
        {
            return [$"node: not installed. Install Node.js {pinned} (see .nvmrc)."];
        }

        ProcessResult result = await processRunner.RunAsync(new ProcessRequest("node", ["--version"]), cancellationToken);
        string installed = result.StandardOutput.Trim().TrimStart('v');

        return installed == pinned ? [] : [$"node: version mismatch - local {installed}, .nvmrc pins {pinned}."];
    }

    private async Task<IReadOnlyList<string>> CheckPnpmAsync(CancellationToken cancellationToken)
    {
        string pinned = await pinnedVersionReader.GetPinnedPnpmVersionAsync(cancellationToken);

        if (!localToolLocator.IsOnPath("pnpm"))
        {
            return
            [
                "pnpm: not found on PATH. Install Node.js (see .nvmrc), then run `corepack enable` "
                    + "to activate pnpm at the version package.json pins.",
            ];
        }

        ProcessResult result = await processRunner.RunAsync(new ProcessRequest("pnpm", ["--version"]), cancellationToken);
        string installed = result.StandardOutput.Trim();

        return installed == pinned ? [] : [$"pnpm: version mismatch - local {installed}, package.json pins {pinned}."];
    }
}
