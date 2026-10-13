// <copyright file="PinnedVersionReader.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Repo;

internal sealed class PinnedVersionReader(RepoPaths paths) : IPinnedVersionReader
{
    private const string PnpmPrefix = "pnpm@";

    public async Task<string> GetPinnedNodeVersionAsync(CancellationToken cancellationToken)
    {
        string content = await File.ReadAllTextAsync(Path.Combine(paths.Root, ".nvmrc"), cancellationToken);
        return content.Trim();
    }

    public async Task<string> GetPinnedPnpmVersionAsync(CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(Path.Combine(paths.Root, "package.json"));
        JsonNode packageJson = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("package.json is empty or not valid JSON.");
        string packageManager = packageJson["packageManager"]?.GetValue<string>()
            ?? throw new InvalidOperationException("package.json has no 'packageManager' field.");

        return packageManager.StartsWith(PnpmPrefix, StringComparison.Ordinal)
            ? packageManager[PnpmPrefix.Length..]
            : throw new InvalidOperationException($"Unexpected 'packageManager' format: '{packageManager}'.");
    }
}
