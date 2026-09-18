// <copyright file="SchemaMarkerStoreTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Testing.Common;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Hj.EShop.Migrations.Orchestration.Tests;

// Needs a real SQL Server - see TestSqlConnectionString. Excluded from the default
// `eshop test`/`dotnet test` run via --filter-not-trait "Category=Integration" (see
// UnitTestCommand in EShop.Cli), same as this repo's other Integration-tagged tests.
[Trait("Category", "Integration")]
public sealed class SchemaMarkerStoreTests
{
    [Fact]
    public async Task GetLatestAsync_NoMarkerWritten_ReturnsNull()
    {
        string component = $"test-component-{Guid.NewGuid():N}";
        await using SqlConnection connection = await OpenConnectionAsync();
        await SchemaMarkerStore.EnsureTableExistsAsync(connection, TestContext.Current.CancellationToken);

        SchemaMarker? marker = await SchemaMarkerStore.GetLatestAsync(connection, component, TestContext.Current.CancellationToken);

        Assert.Null(marker);
    }

    [Fact]
    public async Task WriteAsync_ThenGetLatestAsync_RoundTrips()
    {
        string component = $"test-component-{Guid.NewGuid():N}";
        await using SqlConnection connection = await OpenConnectionAsync();
        await SchemaMarkerStore.EnsureTableExistsAsync(connection, TestContext.Current.CancellationToken);
        SchemaMarker written = new(component, "20260101000000_Initial", DateTime.UtcNow, Succeeded: true, FailureMessage: null);

        await SchemaMarkerStore.WriteAsync(connection, written, TestContext.Current.CancellationToken);
        SchemaMarker? read = await SchemaMarkerStore.GetLatestAsync(connection, component, TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.Equal(written.Component, read.Component);
        Assert.Equal(written.SchemaVersion, read.SchemaVersion);
        Assert.Equal(written.Succeeded, read.Succeeded);
        Assert.Null(read.FailureMessage);
    }

    [Fact]
    public async Task WriteAsync_CalledTwiceForSameComponent_OverwritesRatherThanDuplicates()
    {
        string component = $"test-component-{Guid.NewGuid():N}";
        await using SqlConnection connection = await OpenConnectionAsync();
        await SchemaMarkerStore.EnsureTableExistsAsync(connection, TestContext.Current.CancellationToken);
        await SchemaMarkerStore.WriteAsync(
            connection,
            new SchemaMarker(component, "20260101000000_Initial", DateTime.UtcNow, Succeeded: false, "boom"),
            TestContext.Current.CancellationToken);

        await SchemaMarkerStore.WriteAsync(
            connection,
            new SchemaMarker(component, "20260102000000_Second", DateTime.UtcNow, Succeeded: true, FailureMessage: null),
            TestContext.Current.CancellationToken);
        SchemaMarker? read = await SchemaMarkerStore.GetLatestAsync(connection, component, TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.Equal("20260102000000_Second", read.SchemaVersion);
        Assert.True(read.Succeeded);
    }

    private static async Task<SqlConnection> OpenConnectionAsync()
    {
        SqlConnection connection = new(TestSqlConnectionString.RequireOrSkip());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }
}
