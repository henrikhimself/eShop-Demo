// <copyright file="OptimizelyPackageScriptLocator.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Migrations.Optimizely;

// Locates a package's schema scripts under its tools/ folder: a baseline full-schema script, plus incrementals under one or more "epiupdates*/sql" subfolders.
// See doc/CHRONICLE.md — a Commerce package's own "epiupdates_CMS" folder targets the CMS database, not Commerce.
// Takes the resolved tools/ folder path as a parameter rather than deriving it from the NuGet cache, so this class has no dependency on how that resolution happens.
public static class OptimizelyPackageScriptLocator
{
    public static OptimizelyPackageScripts Locate(
        string toolsDirectoryPath, string baselineScriptFileName, params string[] incrementalFolderNames)
    {
        string baselinePath = Path.Combine(toolsDirectoryPath, baselineScriptFileName);
        FileInfo? baseline = File.Exists(baselinePath) ? new FileInfo(baselinePath) : null;

        List<FileInfo> incrementals = [];
        foreach (string folderName in incrementalFolderNames)
        {
            string sqlFolder = Path.Combine(toolsDirectoryPath, folderName, "sql");
            if (Directory.Exists(sqlFolder))
            {
                incrementals.AddRange(new DirectoryInfo(sqlFolder).GetFiles("*.sql"));
            }
        }

        return new OptimizelyPackageScripts(baseline, SortByVersion(incrementals));
    }

    // Each incremental script's file name is a version number (e.g. "12.23.2.sql").
    public static IReadOnlyList<FileInfo> SortByVersion(IEnumerable<FileInfo> files)
    {
        return files
            .Select(file => (File: file, Version: ParseVersion(file.Name)))
            .OrderBy(item => item.Version)
            .Select(item => item.File)
            .ToList();
    }

    private static Version ParseVersion(string fileName)
    {
        string versionText = Path.GetFileNameWithoutExtension(fileName);
        if (!Version.TryParse(versionText, out Version? version))
        {
            throw new ArgumentException($"File name must be a valid version number: {fileName}");
        }

        return version;
    }
}
