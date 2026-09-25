// <copyright file="LocalToolEnvironment.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Repo;

namespace Hj.EShop.Cli.Execution;

// Single source of truth for the environment variables a locally-run dev tool needs
// pointed at the repo's .cache/ sandbox instead of the developer's real $HOME.
// ToolExecutor.RunLocalAsync and Commands.EnvCommand both read this list so the two
// can never drift apart.
internal static class LocalToolEnvironment
{
    public static IReadOnlyList<KeyValuePair<string, string>> Build(RepoPaths paths)
    {
        return
        [
            new("SSL_CERT_DIR", Path.Combine(paths.CacheHomeDir, ".aspnet", "dev-certs", "trust")),
            new("HOME", paths.CacheHomeDir),
            new("DOTNET_CLI_HOME", paths.CacheHomeDir),
            new("NUGET_PACKAGES", paths.NuGetPackagesDir),
            new("npm_config_cache", paths.NpmCacheDir),
            new("PNPM_CONFIG_STORE_DIR", paths.PnpmStoreDir),
            new("PNPM_HOME", paths.PnpmHomeDir),
            new("COREPACK_ENABLE_DOWNLOAD_PROMPT", "0"),
        ];
    }
}
