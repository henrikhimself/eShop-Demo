// <copyright file="RealOptimizelyScriptsTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Migrations.Orchestration;
using Hj.EShop.Testing.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hj.EShop.StoreFront.MigrationRunner.Tests;

// Runs the *real* EPiServer.CMS.Core/EPiServer.Commerce.Core shipped scripts (via
// CopyOptimizelySchemaScripts.targets, imported by this project's own .csproj), not the
// synthetic fixture ones StorefrontSchemaMigratorTests uses - so a future version bump
// of either package has a real test to fail against, not just the mechanics. Needs a
// real SQL Server - see TestSqlConnectionString. Excluded from the default `eshop test`
// run via --filter-not-trait "Category=Integration".
[Trait("Category", "Integration")]
public sealed class RealOptimizelyScriptsTests : IAsyncLifetime
{
    private string _masterConnectionString = string.Empty;
    private string _cmsDatabaseName = string.Empty;
    private string _commerceDatabaseName = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _masterConnectionString = TestSqlConnectionString.RequireOrSkip();
        string suffix = Guid.NewGuid().ToString("N");
        _cmsDatabaseName = $"real_storefront_cms_test_{suffix}";
        _commerceDatabaseName = $"real_storefront_commerce_test_{suffix}";

        await using SqlConnection master = new(_masterConnectionString);
        await master.OpenAsync(TestContext.Current.CancellationToken);
        await using SqlCommand create = master.CreateCommand();
        create.CommandText = $"CREATE DATABASE [{_cmsDatabaseName}]; CREATE DATABASE [{_commerceDatabaseName}]";
        await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cmsDatabaseName.Length == 0)
        {
            return;
        }

        await using SqlConnection master = new(_masterConnectionString);
        await master.OpenAsync(TestContext.Current.CancellationToken);
        await using SqlCommand drop = master.CreateCommand();
        drop.CommandText =
            $"ALTER DATABASE [{_cmsDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_cmsDatabaseName}]; "
            + $"ALTER DATABASE [{_commerceDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_commerceDatabaseName}]";
        await drop.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RunAsync_RealCmsScripts_AppliesFullSchema()
    {
        StorefrontMigrationOptions options = CreateOptions(StorefrontComponent.Cms, _cmsDatabaseName, "Cms", "EPiServer.Cms.Core.sql", "13.1.3", "epiupdates", "epiupdates_CMS");

        int exitCode = await StorefrontSchemaMigrator.RunAsync(options, NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        await using SqlConnection connection = new(options.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.True(await TableExistsAsync(connection, "tblContent"));
        SchemaMarker? marker = await SchemaMarkerStore.GetLatestAsync(connection, options.ComponentName, TestContext.Current.CancellationToken);
        Assert.NotNull(marker);
        Assert.True(marker.Succeeded);
    }

    [Fact]
    public async Task RunAsync_RealCommerceScripts_AppliesFullSchema()
    {
        StorefrontMigrationOptions options = CreateOptions(
            StorefrontComponent.Commerce, _commerceDatabaseName, "Commerce", "EPiServer.Commerce.Core.sql", "15.2.0", "epiupdates_commerce");

        int exitCode = await StorefrontSchemaMigrator.RunAsync(options, NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        await using SqlConnection connection = new(options.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.True(await TableExistsAsync(connection, "CatalogEntry"));
        SchemaMarker? marker = await SchemaMarkerStore.GetLatestAsync(connection, options.ComponentName, TestContext.Current.CancellationToken);
        Assert.NotNull(marker);
        Assert.True(marker.Succeeded);
    }

    private StorefrontMigrationOptions CreateOptions(
        StorefrontComponent component,
        string databaseName,
        string toolsSubfolder,
        string baselineScriptFileName,
        string targetVersion,
        params string[] incrementalFolderNames)
    {
        SqlConnectionStringBuilder builder = new(_masterConnectionString) { InitialCatalog = databaseName };
        string toolsDirectory = Path.Combine(AppContext.BaseDirectory, "RealOptimizelySchemaScripts", toolsSubfolder);

        (string componentName, string lockName) = component == StorefrontComponent.Cms
            ? ("real-storefront-cms-db", "real-storefront-cms-db-migration")
            : ("real-storefront-commerce-db", "real-storefront-commerce-db-migration");

        return new StorefrontMigrationOptions(
            component, componentName, lockName, builder.ConnectionString, toolsDirectory, baselineScriptFileName, incrementalFolderNames, targetVersion);
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string tableName)
    {
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = @tableName";
        command.Parameters.AddWithValue("@tableName", tableName);
        return (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))! > 0;
    }
}
