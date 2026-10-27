// <copyright file="PinnedVersionReaderTests.cs" company="Henrik Jensen">
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
using Xunit;

namespace Hj.EShop.Cli.Tests.Repo;

// Reads the repo's own real .nvmrc/package.json rather than fixtures - these ARE the
// pinned-version source files (AGENTS.md's "code is the authority" rule); a fixture
// copy would just be a second place for the pin to drift from.
public sealed class PinnedVersionReaderTests
{
    private readonly RepoPaths _paths = new(new RepoRootLocator().Find());

    [Fact]
    public async Task GetPinnedNodeVersionAsync_ReturnsNonEmptyVersion()
    {
        PinnedVersionReader reader = new(_paths);

        string version = await reader.GetPinnedNodeVersionAsync(TestContext.Current.CancellationToken);

        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }

    [Fact]
    public async Task GetPinnedPnpmVersionAsync_ReturnsVersionWithoutPnpmPrefix()
    {
        PinnedVersionReader reader = new(_paths);

        string version = await reader.GetPinnedPnpmVersionAsync(TestContext.Current.CancellationToken);

        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }
}
