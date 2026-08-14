// <copyright file="LocalToolLocatorTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Execution;
using Xunit;

namespace Hj.EShop.Cli.Tests.Execution;

// Points at a real temp directory instead of stubbing the filesystem - matches this
// project's "test against real files" convention (see RepoRootLocatorTests).
public sealed class LocalToolLocatorTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("eshop-cli-tests-").FullName;

    [Fact]
    public void IsOnPath_ToolPresentInSearchPath_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_directory, "footool"), string.Empty);
        LocalToolLocator locator = new([_directory]);

        Assert.True(locator.IsOnPath("footool"));
    }

    [Fact]
    public void IsOnPath_ToolAbsentFromSearchPath_ReturnsFalse()
    {
        LocalToolLocator locator = new([_directory]);

        Assert.False(locator.IsOnPath("no-such-tool"));
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
