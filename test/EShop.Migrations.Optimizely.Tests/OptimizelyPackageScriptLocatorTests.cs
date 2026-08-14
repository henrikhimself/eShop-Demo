// <copyright file="OptimizelyPackageScriptLocatorTests.cs" company="Henrik Jensen">
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

using Xunit;

namespace Hj.EShop.Migrations.Optimizely.Tests;

public sealed class OptimizelyPackageScriptLocatorTests : IDisposable
{
    private readonly string _toolsDirectory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"eshop-optimizely-tools-{Guid.NewGuid():N}")).FullName;

    [Fact]
    public void Locate_BaselineExists_ReturnsFileInfo()
    {
        File.WriteAllText(Path.Combine(_toolsDirectory, "EPiServer.Cms.Core.sql"), "-- baseline");

        OptimizelyPackageScripts scripts = OptimizelyPackageScriptLocator.Locate(_toolsDirectory, "EPiServer.Cms.Core.sql", "epiupdates");

        Assert.NotNull(scripts.BaselineScript);
        Assert.Equal("EPiServer.Cms.Core.sql", scripts.BaselineScript.Name);
    }

    [Fact]
    public void Locate_BaselineMissing_ReturnsNull()
    {
        OptimizelyPackageScripts scripts = OptimizelyPackageScriptLocator.Locate(_toolsDirectory, "EPiServer.Cms.Core.sql", "epiupdates");

        Assert.Null(scripts.BaselineScript);
    }

    [Fact]
    public void Locate_IncrementalFolderMissing_ReturnsEmptyIncrementalList()
    {
        OptimizelyPackageScripts scripts = OptimizelyPackageScriptLocator.Locate(_toolsDirectory, "EPiServer.Cms.Core.sql", "epiupdates");

        Assert.Empty(scripts.IncrementalScripts);
    }

    [Fact]
    public void Locate_IncrementalScriptsAcrossMultipleFolders_ReturnsAllSortedByVersion()
    {
        CreateScript("epiupdates", "12.20.0.sql");
        CreateScript("epiupdates", "12.19.0.sql");
        CreateScript("epiupdates_CMS", "12.19.5.sql");

        OptimizelyPackageScripts scripts = OptimizelyPackageScriptLocator.Locate(
            _toolsDirectory, "EPiServer.Cms.Core.sql", "epiupdates", "epiupdates_CMS");

        Assert.Equal(["12.19.0.sql", "12.19.5.sql", "12.20.0.sql"], scripts.IncrementalScripts.Select(f => f.Name));
    }

    [Fact]
    public void SortByVersion_UnsortedFiles_ReturnsAscendingOrder()
    {
        FileInfo[] files =
        [
            CreateScript("epiupdates", "12.23.2.sql"),
            CreateScript("epiupdates", "10.1.0.sql"),
            CreateScript("epiupdates", "12.2.1.sql"),
        ];

        IReadOnlyList<FileInfo> sorted = OptimizelyPackageScriptLocator.SortByVersion(files);

        Assert.Equal(["10.1.0.sql", "12.2.1.sql", "12.23.2.sql"], sorted.Select(f => f.Name));
    }

    [Fact]
    public void SortByVersion_FileNameNotAVersion_Throws()
    {
        FileInfo[] files = [CreateScript("epiupdates", "not-a-version.sql")];

        Assert.Throws<ArgumentException>(() => OptimizelyPackageScriptLocator.SortByVersion(files));
    }

    private FileInfo CreateScript(string folderName, string fileName)
    {
        string folder = Directory.CreateDirectory(Path.Combine(_toolsDirectory, folderName, "sql")).FullName;
        string path = Path.Combine(folder, fileName);
        File.WriteAllText(path, "-- script");
        return new FileInfo(path);
    }

    public void Dispose()
    {
        Directory.Delete(_toolsDirectory, recursive: true);
    }
}
