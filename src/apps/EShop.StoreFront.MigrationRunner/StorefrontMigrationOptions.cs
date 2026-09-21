// <copyright file="StorefrontMigrationOptions.cs" company="Henrik Jensen">
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

using Hj.EShop.Common;
using Hj.EShop.Migrations.Common;
using Hj.EShop.Migrations.Optimizely;
using Microsoft.Extensions.Configuration;

namespace Hj.EShop.StoreFront.MigrationRunner;

// TargetVersion comes from the installed assembly (OptimizelyInstalledVersion), not
// configuration, so it cannot drift from the Directory.Packages.props pin.
internal sealed record StorefrontMigrationOptions(
    StorefrontComponent Component,
    string ComponentName,
    string LockName,
    string ConnectionString,
    string ToolsDirectory,
    string BaselineScriptFileName,
    IReadOnlyList<string> IncrementalFolderNames,
    string TargetVersion)
{
    public static StorefrontMigrationOptions FromConfiguration(StorefrontComponent component, IConfiguration configuration)
    {
        string connectionStringName = component == StorefrontComponent.Cms
            ? KnownNames.ResourceStorefrontCmsDb
            : KnownNames.ResourceStorefrontCommerceDb;
        string connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Missing connection string '{connectionStringName}'.");

        IConfigurationSection section = configuration.GetSection("StorefrontMigration")
            .GetSection(component == StorefrontComponent.Cms ? "Cms" : "Commerce");

        string configuredToolsDirectory = section["ToolsDirectory"]
            ?? throw new InvalidOperationException($"Missing StorefrontMigration:{component}:ToolsDirectory configuration value.");

        // Resolved against the app's base directory, not the current working directory -
        // CopyOptimizelySchemaScripts copies scripts there, and the two don't always
        // match (e.g. `dotnet run` from the project directory).
        string toolsDirectory = Path.Combine(AppContext.BaseDirectory, configuredToolsDirectory);
        string baselineScriptFileName = section["BaselineScriptFileName"]
            ?? throw new InvalidOperationException($"Missing StorefrontMigration:{component}:BaselineScriptFileName configuration value.");
        string targetVersion = component == StorefrontComponent.Cms
            ? OptimizelyInstalledVersion.Get("EPiServer.Data")
            : OptimizelyInstalledVersion.Get("Mediachase.Commerce");
        string[] incrementalFolderNames = section.GetSection("IncrementalFolderNames").Get<string[]>()
            ?? throw new InvalidOperationException($"Missing StorefrontMigration:{component}:IncrementalFolderNames configuration value.");

        (string componentName, string lockName) = component == StorefrontComponent.Cms
            ? (MigrationNames.StorefrontCmsComponent, MigrationNames.StorefrontCmsLockName)
            : (MigrationNames.StorefrontCommerceComponent, MigrationNames.StorefrontCommerceLockName);

        return new StorefrontMigrationOptions(
            component, componentName, lockName, connectionString, toolsDirectory, baselineScriptFileName, incrementalFolderNames, targetVersion);
    }
}
