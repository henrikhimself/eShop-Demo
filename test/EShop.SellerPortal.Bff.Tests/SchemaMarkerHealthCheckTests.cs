// <copyright file="SchemaMarkerHealthCheckTests.cs" company="Henrik Jensen">
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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// SchemaMarkerHealthCheck casts to SqlConnection, so this needs a real SQL Server (see
// TestSqlConnectionString below) - it can't use SellerPortalWebApplicationFactory's
// SQLite-backed context, unlike the rest of this test project. Excluded from the
// default `eshop test`/`dotnet test` run via --filter-not-trait "Category=Integration"
// (see UnitTestCommand in EShop.Cli), same as this repo's other Integration-tagged
// tests.
[Trait("Category", "Integration")]
public sealed class SchemaMarkerHealthCheckTests : IAsyncLifetime
{
    private string _masterConnectionString = string.Empty;
    private string _databaseName = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _masterConnectionString = TestSqlConnectionString.RequireOrSkip();
        _databaseName = $"schemamarkerhealthcheck_test_{Guid.NewGuid():N}";

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
    public async Task CheckHealthAsync_NoMarkerTableYet_ReturnsUnhealthy()
    {
        HealthCheckResult result = await CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_TableExistsButNoMarkerWritten_ReturnsUnhealthy()
    {
        await using SellerPortalDbContext setupContext = CreateDbContext();
        await EnsureMarkerTableExistsAsync(setupContext);

        HealthCheckResult result = await CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_LastAttemptFailed_ReturnsUnhealthy()
    {
        await using SellerPortalDbContext setupContext = CreateDbContext();
        string currentVersion = setupContext.Database.GetMigrations().LastOrDefault() ?? string.Empty;
        await WriteMarkerAsync(setupContext, new SchemaMarker(
            MigrationNames.SellerPortalComponent, currentVersion, DateTime.UtcNow, Succeeded: false, "boom"));

        HealthCheckResult result = await CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_MarkerVersionOlderThanExpected_ReturnsUnhealthy()
    {
        await using SellerPortalDbContext setupContext = CreateDbContext();
        await WriteMarkerAsync(setupContext, new SchemaMarker(
            MigrationNames.SellerPortalComponent, "some-older-migration", DateTime.UtcNow, Succeeded: true, FailureMessage: null));

        HealthCheckResult result = await CheckHealthAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_MarkerCurrentAndSucceeded_ReturnsHealthy()
    {
        await using SellerPortalDbContext setupContext = CreateDbContext();
        string currentVersion = setupContext.Database.GetMigrations().LastOrDefault() ?? string.Empty;
        await WriteMarkerAsync(setupContext, new SchemaMarker(
            MigrationNames.SellerPortalComponent, currentVersion, DateTime.UtcNow, Succeeded: true, FailureMessage: null));

        HealthCheckResult result = await CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private async Task<HealthCheckResult> CheckHealthAsync()
    {
        await using SellerPortalDbContext dbContext = CreateDbContext();
        SchemaMarkerHealthCheck healthCheck = new(dbContext);
        return await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
    }

    private static async Task EnsureMarkerTableExistsAsync(SellerPortalDbContext dbContext)
    {
        await dbContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        try
        {
            await SchemaMarkerStore.EnsureTableExistsAsync(
                (SqlConnection)dbContext.Database.GetDbConnection(), TestContext.Current.CancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task WriteMarkerAsync(SellerPortalDbContext dbContext, SchemaMarker marker)
    {
        await dbContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            await SchemaMarkerStore.EnsureTableExistsAsync(connection, TestContext.Current.CancellationToken);
            await SchemaMarkerStore.WriteAsync(connection, marker, TestContext.Current.CancellationToken);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private SellerPortalDbContext CreateDbContext()
    {
        SqlConnectionStringBuilder builder = new(_masterConnectionString) { InitialCatalog = _databaseName };
        DbContextOptions<SellerPortalDbContext> options = new DbContextOptionsBuilder<SellerPortalDbContext>()
            .UseSqlServer(builder.ConnectionString)
            .Options;
        return new SellerPortalDbContext(options);
    }
}
