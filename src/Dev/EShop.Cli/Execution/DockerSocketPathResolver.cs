// <copyright file="DockerSocketPathResolver.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Execution;

// Configurable via ESHOP_DOCKER, env-var-only (no CLI flag - mirrors the env-var half
// of ESHOP_TOOLS/ESHOP_AGENT's flag/env-var/default precedence in
// GlobalOptionsInterceptor). Needed by both LinuxIdentityProvider (stats the socket
// for its gid) and ContainerRunner (bind-mounts it into the utility container), so
// it's centralized here rather than duplicated as a literal in both.
//
// Resolution falls back through four sources, in priority order. First, an explicit
// ESHOP_DOCKER override always wins when set. Next comes DOCKER_HOST, stripped of a
// leading unix:// scheme - the standard Docker CLI/SDK convention, so a host that
// already sets it (e.g. rootless Docker's own setup script) is picked up
// automatically. After that comes XDG_RUNTIME_DIR's own docker.sock file - rootless
// Docker's default socket location - but only when that file actually exists, so an
// unrelated XDG_RUNTIME_DIR (e.g. one set for other tooling but with no Docker socket
// in it) doesn't shadow a working rootful setup. The final fallback is
// /var/run/docker.sock, today's default, keeping WSL/Docker Desktop/rootful-Linux
// developers unchanged.
internal static class DockerSocketPathResolver
{
    private const string EshopDockerVariable = "ESHOP_DOCKER";
    private const string DockerHostVariable = "DOCKER_HOST";
    private const string XdgRuntimeDirVariable = "XDG_RUNTIME_DIR";
    private const string UnixSchemePrefix = "unix://";
    private const string Default = "/var/run/docker.sock";

    public static string Resolve()
    {
        string? eshopDocker = Environment.GetEnvironmentVariable(EshopDockerVariable);
        if (!string.IsNullOrEmpty(eshopDocker))
        {
            return eshopDocker;
        }

        string? dockerHost = Environment.GetEnvironmentVariable(DockerHostVariable);
        if (!string.IsNullOrEmpty(dockerHost))
        {
            return dockerHost.StartsWith(UnixSchemePrefix, StringComparison.Ordinal)
                ? dockerHost[UnixSchemePrefix.Length..]
                : dockerHost;
        }

        string? xdgRuntimeDir = Environment.GetEnvironmentVariable(XdgRuntimeDirVariable);
        if (!string.IsNullOrEmpty(xdgRuntimeDir))
        {
            string candidate = Path.Combine(xdgRuntimeDir, "docker.sock");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Default;
    }
}
