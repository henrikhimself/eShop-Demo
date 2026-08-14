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

// Locates the schema scripts a single EPiServer.* NuGet package ships under its own
// tools/ folder - the baseline full-schema script, plus every incremental script under
// one or more "epiupdates*/sql" subfolders (e.g. "epiupdates" for CMS,
// "epiupdates_Commerce"/"epiupdates_CMS" for Commerce - the latter targets the CMS
// database despite shipping inside the Commerce package, per
// EPiServer.Net.Cli's UpdateDatabase.RunAsync). Takes the package's tools/ folder path
// directly (already resolved by the caller/build step) rather than re-deriving it from a
// NuGet cache location itself, so this class has no dependency on how that resolution
// happens - see doc/adr/0023-explicit-database-schema-migration-resources.md.
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

    // Each incremental script's file name is a version number (e.g. "12.23.2.sql") -
    // mirrors EPiServer.Net.Cli's UpdateDatabase.Sort.
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
