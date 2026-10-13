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

// Locates baseline and incremental scripts under a package's tools folder.
// A Commerce package's epiupdates_CMS folder targets the CMS database, not Commerce.
// Takes the resolved tools folder path to avoid depending on NuGet-cache resolution.
public static class OptimizelyPackageScriptLocator
{
    public static OptimizelyPackageScripts Locate(
        string toolsDirectoryPath, string baselineScriptFileName, params string[] incrementalFolderNames)
    {
        if (!Directory.Exists(toolsDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Optimizely tools directory was not found: {toolsDirectoryPath}");
        }

        string baselinePath = Path.Combine(toolsDirectoryPath, baselineScriptFileName);
        if (!File.Exists(baselinePath))
        {
            throw new FileNotFoundException($"Optimizely baseline script was not found: {baselinePath}", baselinePath);
        }

        List<FileInfo> incrementals = [];
        foreach (string folderName in incrementalFolderNames)
        {
            string sqlFolder = Path.Combine(toolsDirectoryPath, folderName, "sql");
            if (!Directory.Exists(sqlFolder))
            {
                throw new DirectoryNotFoundException($"Optimizely incremental script directory was not found: {sqlFolder}");
            }

            incrementals.AddRange(new DirectoryInfo(sqlFolder).GetFiles("*.sql"));
        }

        return new OptimizelyPackageScripts(new FileInfo(baselinePath), SortByVersion(incrementals));
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
