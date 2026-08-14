// <copyright file="UtilityImageProvisioner.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Repo;

namespace Hj.EShop.Cli.Execution;

// Func<IOutputSink>, not IOutputSink: this class is built before
// GlobalOptionsAccessor.Resolve(...) runs, and the factory is only called from
// EnsureBuiltAsync's rare "image missing/stale" branch, well after that point.
internal sealed class UtilityImageProvisioner(
    IProcessRunner processRunner, RepoPaths paths, Func<IOutputSink> outputFactory, IUtilityImageSourceHasher sourceHasher)
    : IUtilityImageProvisioner
{
    // Docker label the built image's own content hash is stamped with - read back on
    // the next invocation to decide staleness by content, not just tag presence.
    private const string SourceHashLabel = "eshop.source-hash";

    public async Task EnsureBuiltAsync(CancellationToken cancellationToken)
    {
        string currentHash = await sourceHasher.ComputeAsync(cancellationToken);

        ProcessResult inspect = await processRunner.RunAsync(
            new ProcessRequest(
                "docker",
                ["image", "inspect", "--format", $"{{{{ index .Config.Labels \"{SourceHashLabel}\" }}}}", RepoPaths.UtilityImageTag]),
            cancellationToken);

        if (inspect.Succeeded && inspect.StandardOutput.Trim() == currentHash)
        {
            return;
        }

        IOutputSink output = outputFactory();
        output.Text($"Building utility image '{RepoPaths.UtilityImageTag}' from '{paths.ContainerfilePath}'.");

        ProcessResult build = await processRunner.RunAsync(
            new ProcessRequest(
                "docker",
                ["build", "--label", $"{SourceHashLabel}={currentHash}", "-t", RepoPaths.UtilityImageTag, "-f", paths.ContainerfilePath, paths.Root]),
            cancellationToken);

        if (!build.Succeeded)
        {
            throw new InvalidOperationException($"Failed to build utility image '{RepoPaths.UtilityImageTag}': {build.StandardError}");
        }

        output.Text($"Utility image '{RepoPaths.UtilityImageTag}' built.");
    }
}
