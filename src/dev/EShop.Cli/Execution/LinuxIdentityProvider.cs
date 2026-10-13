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

// getuid/getgid use source-generated marshalling; stat reads the socket group because it has no BCL equivalent.
// Rootless Docker uses uid/gid 0 without a socket group: the host-reported gid is meaningless in the container's id space.
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
