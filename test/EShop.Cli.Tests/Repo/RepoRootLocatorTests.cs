// <copyright file="RepoRootLocatorTests.cs" company="Henrik Jensen">
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

public sealed class RepoRootLocatorTests
{
    [Fact]
    public void Find_FromTestBinaryDirectory_LocatesRepoRootContainingSolutionFile()
    {
        RepoRootLocator locator = new();

        string root = locator.Find();

        Assert.True(File.Exists(Path.Combine(root, "EShop.slnx")));
        Assert.True(Directory.Exists(Path.Combine(root, "src", "EShop.Cli")));
    }
}
