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

// One resolution policy shared by socket ownership and bind mounting.
// Priority: ESHOP_DOCKER, DOCKER_HOST, an existing XDG_RUNTIME_DIR socket, then the default.
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
