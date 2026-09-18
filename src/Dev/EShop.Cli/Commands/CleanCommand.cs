// <copyright file="CleanCommand.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;
using Spectre.Console.Cli;

namespace Hj.EShop.Cli.Commands;

// Resets local build state: bin/obj, Next.js build caches, and tmp/ (build output
// only - downloaded dependency caches live under .cache/ and are never touched, see
// doc/CHRONICLE.md). Never touches node_modules, .cache/, or the utility Docker image.
internal sealed class CleanCommand(IOutputSink output, IAppHostGuard appHostGuard, RepoPaths paths)
    : AsyncCommand<DefaultSettings>
{
    private static readonly string[] _directoryNamesToSkip = ["node_modules", ".git", "tmp", ".cache"];

    protected override async Task<int> ExecuteAsync(CommandContext context, DefaultSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "Clean Result");

        if (await appHostGuard.IsAlreadyRunningAsync(cancellationToken))
        {
            output.Status(Severity.Failure, "An aspire session for this AppHost is already running. Stop it and then re-run this command.");
            return 1;
        }

        // Independent - the bin/obj sweep, the Next.js caches, and tmp/ touch disjoint
        // paths on disk.
        Task<int> binObjTask = DeleteBinObjDirectoriesAsync(cancellationToken);
        Task<bool> nextTask = DeleteNextCachesAsync(cancellationToken);
        Task<bool> tmpTask = DeleteTmpDirAsync(cancellationToken);
        await Task.WhenAll(binObjTask, nextTask, tmpTask);

        // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
        await output.FlushPendingStepsAsync();

        output.Text($"Deleted {binObjTask.Result} bin/obj director{(binObjTask.Result == 1 ? "y" : "ies")}.");
        output.Text(nextTask.Result ? "Deleted Next.js build caches." : "Next.js build caches were already clean.");
        output.Text(tmpTask.Result ? "Deleted tmp/." : "tmp/ was already clean.");

        return 0;
    }

    private async Task<int> DeleteBinObjDirectoriesAsync(CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep("Delete bin/obj directories");
        return await Task.Run(() => FindBinObjDirectories(paths.Root).Count(TryDeleteDirectory), cancellationToken);
    }

    private async Task<bool> DeleteNextCachesAsync(CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep("Delete Next.js build caches");
        return await Task.Run(
            () =>
            {
                bool deletedAny = TryDeleteDirectory(Path.Combine(paths.SellerPortalWebDir, ".next"));
                deletedAny |= TryDeleteDirectory(Path.Combine(paths.SellerPortalWebDir, ".next-container"));
                return deletedAny;
            },
            cancellationToken);
    }

    private async Task<bool> DeleteTmpDirAsync(CancellationToken cancellationToken)
    {
        using IDisposable step = output.BeginStep("Delete tmp/");
        return await Task.Run(() => TryDeleteDirectory(paths.TmpDir), cancellationToken);
    }

    // Walks the repo tree for directories literally named bin/obj, never descending
    // into node_modules, .git, tmp, or .cache - none of those ever contain a bin/obj
    // to clean, and descending into .cache would pointlessly walk the NuGet/pnpm
    // package caches.
    private static IEnumerable<string> FindBinObjDirectories(string root)
    {
        Stack<string> pending = new();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string current = pending.Pop();
            string[] subdirectories;
            try
            {
                subdirectories = Directory.GetDirectories(current);
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (string subdirectory in subdirectories)
            {
                string name = Path.GetFileName(subdirectory);
                if (_directoryNamesToSkip.Contains(name))
                {
                    continue;
                }

                if (name is "bin" or "obj")
                {
                    yield return subdirectory;
                    continue;
                }

                pending.Push(subdirectory);
            }
        }
    }

    // Tolerates "already gone" as a no-op, not a failure - clean should be safely re-runnable.
    private static bool TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        try
        {
            Directory.Delete(path, recursive: true);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}
