// <copyright file="StorefrontSchemaMigratorTests.cs" company="Henrik Jensen">
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
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hj.EShop.StoreFront.MigrationRunner.Tests;

// Needs a real SQL Server - see TestSqlConnectionString. Excluded from the default
// `eshop test`/`dotnet test` run via --filter-not-trait "Category=Integration" (see
// UnitTestCommand in EShop.Cli). Each test gets its own uniquely-named, genuinely fresh
// (never-migrated) database, same shape as EShop.SellerPortal.MigrationRunner.Tests -
// see doc/adr/0023-explicit-database-schema-migration-resources.md.
[Trait("Category", "Integration")]
public sealed class StorefrontSchemaMigratorTests : IAsyncLifetime
{
    private const string ComponentName = "storefront-migration-test-component";
    private const string LockName = "storefront-migration-test-lock";
    private const string TargetVersion = "1.0.1";

    private string _masterConnectionString = string.Empty;
    private string _databaseName = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _masterConnectionString = TestSqlConnectionString.RequireOrSkip();
        _databaseName = $"storefrontmigration_test_{Guid.NewGuid():N}";

        await using SqlConnection master = new(_masterConnectionString);
        await master.OpenAsync(TestContext.Current.CancellationToken);
        await using SqlCommand create = master.CreateCommand();
        create.CommandText = $"CREATE DATABASE [{_databaseName}]";
        await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_databaseName.Length == 0)
        {
            return;
        }

        await using SqlConnection master = new(_masterConnectionString);
        await master.OpenAsync(TestContext.Current.CancellationToken);
        await using SqlCommand drop = master.CreateCommand();
        drop.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
        await drop.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RunAsync_FreshDatabase_AppliesScriptsAndWritesSuccessMarker()
    {
        int exitCode = await StorefrontSchemaMigrator.RunAsync(CreateOptions(), NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        SchemaMarker? marker = await ReadMarkerAsync();
        Assert.NotNull(marker);
        Assert.True(marker.Succeeded);
        Assert.Equal(TargetVersion, marker.SchemaVersion);
        Assert.Equal(2, await ReadTestSchemaVersionAsync());
    }

    [Fact]
    public async Task RunAsync_AlreadyAtTargetVersion_NoOpsButStillWritesMarker()
    {
        StorefrontMigrationOptions options = CreateOptions();
        await StorefrontSchemaMigrator.RunAsync(options, NullLogger.Instance, TestContext.Current.CancellationToken);

        int secondExitCode = await StorefrontSchemaMigrator.RunAsync(options, NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal(0, secondExitCode);
        SchemaMarker? marker = await ReadMarkerAsync();
        Assert.NotNull(marker);
        Assert.True(marker.Succeeded);
        Assert.Equal(2, await ReadTestSchemaVersionAsync());
    }

    // The actual proof the lock does something: two runners started concurrently against
    // the same never-migrated database. Without the lock, both would race to CREATE
    // TABLE the same schema and at least one would fail; with it, they serialize, and
    // both report success.
    [Fact]
    public async Task RunAsync_TwoConcurrentRunnersAgainstFreshDatabase_BothSucceed()
    {
        StorefrontMigrationOptions options = CreateOptions();

        Task<int> firstRun = StorefrontSchemaMigrator.RunAsync(options, NullLogger.Instance, TestContext.Current.CancellationToken);
        Task<int> secondRun = StorefrontSchemaMigrator.RunAsync(options, NullLogger.Instance, TestContext.Current.CancellationToken);
        int[] exitCodes = await Task.WhenAll(firstRun, secondRun);

        Assert.All(exitCodes, exitCode => Assert.Equal(0, exitCode));
        Assert.Equal(2, await ReadTestSchemaVersionAsync());
    }

    private StorefrontMigrationOptions CreateOptions()
    {
        return new StorefrontMigrationOptions(
            StorefrontComponent.Cms,
            ComponentName,
            LockName,
            DatabaseConnectionString(),
            OptimizelyScriptFixture.CreateToolsDirectory(),
            OptimizelyScriptFixture.BaselineScriptFileName,
            [OptimizelyScriptFixture.IncrementalFolderName],
            TargetVersion);
    }

    private async Task<SchemaMarker?> ReadMarkerAsync()
    {
        await using SqlConnection connection = new(DatabaseConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return await SchemaMarkerStore.GetLatestAsync(connection, ComponentName, TestContext.Current.CancellationToken);
    }

    private async Task<int> ReadTestSchemaVersionAsync()
    {
        await using SqlConnection connection = new(DatabaseConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "dbo.TestSchemaVersion";
        command.CommandType = System.Data.CommandType.StoredProcedure;
        SqlParameter returnValue = command.Parameters.Add("@ReturnValue", System.Data.SqlDbType.Int);
        returnValue.Direction = System.Data.ParameterDirection.ReturnValue;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return (int)returnValue.Value;
    }

    private string DatabaseConnectionString()
    {
        SqlConnectionStringBuilder builder = new(_masterConnectionString) { InitialCatalog = _databaseName };
        return builder.ConnectionString;
    }
}
