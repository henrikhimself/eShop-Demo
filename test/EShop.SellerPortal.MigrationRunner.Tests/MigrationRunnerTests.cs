// <copyright file="MigrationRunnerTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Migrations.Common;
using Hj.EShop.Migrations.Orchestration;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.Testing.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hj.EShop.SellerPortal.MigrationRunner.Tests;

// Needs a real SQL Server - see TestSqlConnectionString. Excluded from the default
// `eshop test`/`dotnet test` run via --filter-not-trait "Category=Integration" (see
// UnitTestCommand in EShop.Cli), same as this repo's other Integration-tagged tests.
// Each test gets its own uniquely-named, genuinely fresh (never-migrated) database, so
// the concurrency test below is a real proof, not one relying on migrations already
// being a no-op.
[Trait("Category", "Integration")]
public sealed class MigrationRunnerTests : IAsyncLifetime
{
    private string _masterConnectionString = string.Empty;
    private string _databaseName = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _masterConnectionString = TestSqlConnectionString.RequireOrSkip();
        _databaseName = $"migrationrunner_test_{Guid.NewGuid():N}";

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
    public async Task RunAsync_FreshDatabase_MigratesAndWritesSuccessMarker()
    {
        SellerPortalDbContext dbContext = CreateDbContext();
        await using (dbContext)
        {
            int exitCode = await SchemaMigrator.RunAsync(dbContext, NullLogger.Instance, TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
        }

        await using SqlConnection connection = new(DatabaseConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        SchemaMarker? marker = await SchemaMarkerStore.GetLatestAsync(
            connection, MigrationNames.SellerPortalComponent, TestContext.Current.CancellationToken);

        Assert.NotNull(marker);
        Assert.True(marker.Succeeded);
        Assert.NotEmpty(marker.SchemaVersion);
    }

    // The actual proof the lock does something: two runners started concurrently
    // against the same never-migrated database. Without the lock, both would race to
    // CREATE TABLE the same schema and at least one would fail; with it, they serialize,
    // and both report success.
    [Fact]
    public async Task RunAsync_TwoConcurrentRunnersAgainstFreshDatabase_BothSucceed()
    {
        SellerPortalDbContext first = CreateDbContext();
        SellerPortalDbContext second = CreateDbContext();
        await using (first)
        await using (second)
        {
            Task<int> firstRun = SchemaMigrator.RunAsync(first, NullLogger.Instance, TestContext.Current.CancellationToken);
            Task<int> secondRun = SchemaMigrator.RunAsync(second, NullLogger.Instance, TestContext.Current.CancellationToken);
            int[] exitCodes = await Task.WhenAll(firstRun, secondRun);

            Assert.All(exitCodes, exitCode => Assert.Equal(0, exitCode));
        }
    }

    private SellerPortalDbContext CreateDbContext()
    {
        DbContextOptions<SellerPortalDbContext> options = new DbContextOptionsBuilder<SellerPortalDbContext>()
            .UseSqlServer(DatabaseConnectionString())
            .Options;
        return new SellerPortalDbContext(options);
    }

    private string DatabaseConnectionString()
    {
        SqlConnectionStringBuilder builder = new(_masterConnectionString) { InitialCatalog = _databaseName };
        return builder.ConnectionString;
    }
}
