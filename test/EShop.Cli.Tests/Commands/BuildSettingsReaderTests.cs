// <copyright file="BuildSettingsReaderTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Commands;
using Hj.EShop.Cli.Repo;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands;

// Reads the repo's own real appsettings.json rather than a fixture - see
// PinnedVersionReaderTests for why (AGENTS.md's "code is the authority" rule).
public sealed class BuildSettingsReaderTests
{
    private readonly RepoPaths _paths = new(new RepoRootLocator().Find());

    [Fact]
    public async Task GetBuildParamsAsync_ReturnsSourceLinkDisablingParams()
    {
        BuildSettingsReader reader = new(_paths);

        IReadOnlyList<string> buildParams = await reader.GetBuildParamsAsync(TestContext.Current.CancellationToken);

        Assert.Contains("/p:EnableSourceControlManagerQueries=false", buildParams);
        Assert.Contains("/p:EnableSourceLink=false", buildParams);
    }
}
