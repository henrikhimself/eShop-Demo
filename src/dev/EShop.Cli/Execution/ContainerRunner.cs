// <copyright file="ContainerRunner.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Repo;
using Hj.EShop.Common;

namespace Hj.EShop.Cli.Execution;

// Docker.sock is always bind-mounted, even for non-interactive runs - some tools run
// inside the utility container (e.g. Testcontainers-based e2e tests) themselves talk
// to the host's Docker daemon (see doc/CHRONICLE.md's "Docker-outside-of-Docker
// networking" section). Its host path is configurable via ESHOP_DOCKER (see
// DockerSocketPathResolver) for hosts where it isn't /var/run/docker.sock, e.g.
// rootless Docker.
internal sealed class ContainerRunner(
    IProcessRunner processRunner,
    IUtilityImageProvisioner imageProvisioner,
    ILinuxIdentityProvider identityProvider,
    RepoPaths paths) : IContainerRunner
{
    public async Task<ProcessResult> RunAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        await imageProvisioner.EnsureBuiltAsync(cancellationToken);

        List<string> arguments = ["run", "--rm"];
        arguments.AddRange(TtyFlags(invocation.Interactive));
        arguments.AddRange(["--network", "host"]);
        foreach (string hostName in KnownValues.ReverseProxyHostNames.Values)
        {
            arguments.AddRange(["--add-host", $"{hostName}:127.0.0.1"]);
        }

        LinuxIdentity? identity = await identityProvider.GetIdentityAsync(cancellationToken);
        if (identity is not null)
        {
            arguments.AddRange(["--user", $"{identity.Uid}:{identity.Gid}"]);
            if (identity.DockerSocketGid is int dockerSocketGid)
            {
                arguments.AddRange(["--group-add", dockerSocketGid.ToString(CultureInfo.InvariantCulture)]);
            }
        }

        arguments.AddRange(["-v", $"{paths.Root}:/workspace", "-w", "/workspace"]);
        string dockerSocketPath = DockerSocketPathResolver.Resolve();
        arguments.AddRange(["-v", $"{dockerSocketPath}:{dockerSocketPath}"]);

        // Tools inside the utility container (docker CLI, Aspire CLI, etc.) default to
        // /var/run/docker.sock themselves unless told otherwise - point them at the
        // mount target (which matches the resolved host path 1:1, since the bind mount
        // above uses the same path on both sides) rather than assuming that default.
        arguments.AddRange(["-e", $"DOCKER_HOST=unix://{dockerSocketPath}"]);
        arguments.AddRange(["--env-file", paths.ContainerEnvPath]);
        if (invocation.EnvironmentVariables is not null)
        {
            foreach (KeyValuePair<string, string> variable in invocation.EnvironmentVariables)
            {
                arguments.AddRange(["-e", $"{variable.Key}={variable.Value}"]);
            }
        }

        arguments.Add(RepoPaths.UtilityImageTag);
        arguments.Add(invocation.Tool);
        arguments.AddRange(invocation.Arguments);

        return await processRunner.RunAsync(
            new ProcessRequest(
                "docker", arguments, invocation.WorkingDirectory,
                Interactive: invocation.Interactive, OutputLineHandler: invocation.OutputLineHandler),
            cancellationToken);
    }

    private static IReadOnlyList<string> TtyFlags(bool interactive)
    {
        if (!interactive)
        {
            return [];
        }

        return Console.IsInputRedirected || Console.IsOutputRedirected ? ["-i"] : ["-i", "-t"];
    }
}
