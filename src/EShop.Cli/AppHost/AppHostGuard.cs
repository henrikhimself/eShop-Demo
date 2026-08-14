// <copyright file="AppHostGuard.cs" company="Henrik Jensen">
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

using System.Text.Json.Nodes;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;

namespace Hj.EShop.Cli.AppHost;

internal sealed class AppHostGuard(IToolExecutor toolExecutor, RepoPaths paths) : IAppHostGuard
{
    public async Task<bool> IsAlreadyRunningAsync(CancellationToken cancellationToken)
    {
        ProcessResult result = await toolExecutor.RunAsync(
            new ToolInvocation("aspire", ["ps", "--format", "Json", "--non-interactive"], WorkingDirectory: paths.Root),
            cancellationToken);

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return false;
        }

        JsonArray? sessions = JsonNode.Parse(result.StandardOutput)?.AsArray();
        if (sessions is null)
        {
            return false;
        }

        // aspire ps can run either on the host or inside the utility container,
        // where the repo is mounted at a different absolute path (/workspace).
        // Compare on the repo-relative, separator-normalized path suffix instead
        // of the full absolute string, so the guard works in both modes.
        string expectedRelativePath = NormalizeSeparators(
            Path.GetRelativePath(paths.Root, paths.AppHostProject));

        return sessions.Any(session =>
            session?["appHostPath"]?.GetValue<string>() is string appHostPath
            && NormalizeSeparators(appHostPath).EndsWith(expectedRelativePath, StringComparison.Ordinal)
            && session?["status"]?.GetValue<string>() == "running");
    }

    private static string NormalizeSeparators(string path)
    {
        return path.Replace('\\', '/');
    }
}
