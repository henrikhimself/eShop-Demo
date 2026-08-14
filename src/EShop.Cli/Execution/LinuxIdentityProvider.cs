// <copyright file="LinuxIdentityProvider.cs" company="Henrik Jensen">
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
using System.Runtime.InteropServices;

namespace Hj.EShop.Cli.Execution;

// uid/gid come from a direct libc call via source-generated marshalling (requires
// <AllowUnsafeBlocks> in the .csproj). The Docker socket's group id has no BCL
// equivalent, so it's read via `stat` through IProcessRunner instead of a hand-rolled
// `stat` struct P/Invoke, whose layout differs across libc/kernel builds.
//
// Rootless Docker is a special case: rootlesskit maps container uid/gid 0 to the
// invoking host user and ids 1-65535 to a subordinate range (e.g. 100000-165535), all
// within the daemon's own user namespace. `stat`-ing the socket from the plain host
// reports its group in that subordinate space (e.g. 100995), which is meaningless
// inside the container's own 0-65535 id space - passing it to `--group-add` makes
// runc's setgroups() fail with EINVAL. There's also no need to resolve it: root inside
// the container (uid 0, mapped to the invoking host user) has DAC-override within its
// own namespace, so it can already read/write the bind-mounted socket regardless of its
// group. Hence: rootless short-circuits straight to uid/gid 0 with no socket gid at all.
internal sealed partial class LinuxIdentityProvider(IProcessRunner processRunner) : ILinuxIdentityProvider
{
    public async Task<LinuxIdentity?> GetIdentityAsync(CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return null;
        }

        if (await IsRootlessDockerAsync(cancellationToken))
        {
            return new LinuxIdentity(0, 0, null);
        }

        int? dockerSocketGid = await ResolveDockerSocketGidAsync(cancellationToken);
        return new LinuxIdentity(getuid(), getgid(), dockerSocketGid);
    }

    [LibraryImport("libc")]
    private static partial int getuid();

    [LibraryImport("libc")]
    private static partial int getgid();

    private async Task<bool> IsRootlessDockerAsync(CancellationToken cancellationToken)
    {
        // Same detection Docker's own tooling uses: rootless dockerd always reports
        // "rootless" among its SecurityOptions.
        ProcessResult result = await processRunner.RunAsync(
            new ProcessRequest("docker", ["info", "--format", "{{.SecurityOptions}}"]),
            cancellationToken);

        return result.Succeeded && result.StandardOutput.Contains("rootless", StringComparison.Ordinal);
    }

    private async Task<int?> ResolveDockerSocketGidAsync(CancellationToken cancellationToken)
    {
        ProcessResult result = await processRunner.RunAsync(
            new ProcessRequest("stat", ["-c", "%g", DockerSocketPathResolver.Resolve()]),
            cancellationToken);

        return result.Succeeded && int.TryParse(result.StandardOutput.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int gid)
            ? gid
            : null;
    }
}
