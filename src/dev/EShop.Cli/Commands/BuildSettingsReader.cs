// <copyright file="BuildSettingsReader.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Repo;

namespace Hj.EShop.Cli.Commands;

internal sealed class BuildSettingsReader(RepoPaths paths) : IBuildSettingsReader
{
    public async Task<IReadOnlyList<string>> GetBuildParamsAsync(CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(paths.CliAppSettingsPath);
        JsonNode appSettings = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("appsettings.json is empty or not valid JSON.");
        JsonArray buildParams = appSettings["build"]?["params"]?.AsArray()
            ?? throw new InvalidOperationException("appsettings.json has no 'build:params' array.");

        return buildParams.Select(param => param!.GetValue<string>()).ToArray();
    }
}
