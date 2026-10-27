// <copyright file="OptimizelyScriptFixture.cs" company="Henrik Jensen">
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

namespace Hj.EShop.StoreFront.MigrationRunner.Tests;

// Builds a synthetic set of scripts shaped exactly like Optimizely's own (a
// self-guarding --BEGINVALIDATINGQUERY block per file, a version-tracking stored
// procedure, GO-separated batches) - lets the migrator's real mechanics be integration
// tested without depending on the actual EPiServer.CMS.Core/EPiServer.Commerce.Core NuGet
// packages being restorable.
internal static class OptimizelyScriptFixture
{
    public const string BaselineScriptFileName = "baseline.sql";

    public const string IncrementalFolderName = "epiupdates";

    // Creates:
    //  - <toolsDirectory>/baseline.sql: creates dbo.TestWidgets and a version-tracking
    //    proc returning 1, self-guarding via the same table-existence check Optimizely's
    //    own baseline script uses.
    //  - <toolsDirectory>/epiupdates/sql/1.0.1.sql: adds a column, bumps the proc to 2.
    public static string CreateToolsDirectory()
    {
        string toolsDirectory = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), $"eshop-storefront-migration-fixture-{Guid.NewGuid():N}")).FullName;

        File.WriteAllText(
            Path.Combine(toolsDirectory, BaselineScriptFileName),
            """
            --BEGINVALIDATINGQUERY
            if exists (select 1 from sys.objects where name = 'TestSchemaVersion' and type = 'P')
                select 0, 'Already installed'
            else
                select 1, 'Ok'
            --ENDVALIDATINGQUERY
            GO
            CREATE TABLE dbo.TestWidgets (Id INT NOT NULL PRIMARY KEY);
            GO
            CREATE PROCEDURE dbo.TestSchemaVersion
            AS
                RETURN 1
            GO
            """);

        string incrementalFolder = Directory.CreateDirectory(
            Path.Combine(toolsDirectory, IncrementalFolderName, "sql")).FullName;
        File.WriteAllText(
            Path.Combine(incrementalFolder, "1.0.1.sql"),
            """
            --BEGINVALIDATINGQUERY
            declare @ver int
            exec @ver = dbo.TestSchemaVersion
            if (@ver >= 2)
                select 0, 'Already correct version'
            else if (@ver = 1)
                select 1, 'Upgrading'
            else
                select -1, 'Invalid version'
            --ENDVALIDATINGQUERY
            GO
            ALTER TABLE dbo.TestWidgets ADD Name NVARCHAR(100) NULL;
            GO
            ALTER PROCEDURE dbo.TestSchemaVersion
            AS
                RETURN 2
            GO
            """);

        return toolsDirectory;
    }
}
