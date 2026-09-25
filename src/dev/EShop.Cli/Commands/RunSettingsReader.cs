// <copyright file="RunSettingsReader.cs" company="Henrik Jensen">
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

internal sealed class RunSettingsReader(RepoPaths paths) : IRunSettingsReader
{
    public async Task<IReadOnlyDictionary<string, string>> GetRunEnvironmentVariablesAsync(CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(paths.CliAppSettingsPath);
        JsonNode appSettings = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("appsettings.json is empty or not valid JSON.");
        JsonArray runParams = appSettings["run"]?["params"]?.AsArray()
            ?? throw new InvalidOperationException("appsettings.json has no 'run:params' array.");

        Dictionary<string, string> environmentVariables = [];
        foreach (JsonNode? param in runParams)
        {
            string entry = param!.GetValue<string>();
            int separatorIndex = entry.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                throw new InvalidOperationException($"appsettings.json's 'run:params' entry '{entry}' is not in 'KEY=VALUE' form.");
            }

            environmentVariables[entry[..separatorIndex]] = entry[(separatorIndex + 1)..];
        }

        return environmentVariables;
    }
}
