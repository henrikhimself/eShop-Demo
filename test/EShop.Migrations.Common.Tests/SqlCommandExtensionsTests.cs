// <copyright file="SqlCommandExtensionsTests.cs" company="Henrik Jensen">
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

using Microsoft.Data.SqlClient;
using Xunit;

namespace Hj.EShop.Migrations.Common.Tests;

// Needs a real SQL Server - see TestSqlConnectionString. Excluded from the default
// `eshop test`/`dotnet test` run via --filter-not-trait "Category=Integration" (see
// UnitTestCommand in EShop.Cli), same as this repo's other Integration-tagged tests.
[Trait("Category", "Integration")]
public sealed class SqlCommandExtensionsTests
{
    [Fact]
    public async Task ExecuteNonQueryAsync_WithParameters_RunsTheCommand()
    {
        await using SqlConnection connection = await OpenConnectionAsync();
        string tableName = $"test_table_{Guid.NewGuid():N}";

        await connection.ExecuteNonQueryAsync(
            $"CREATE TABLE [{tableName}] (Value NVARCHAR(50) NOT NULL)",
            transaction: null,
            configureParameters: null,
            TestContext.Current.CancellationToken);
        int rowsAffected = await connection.ExecuteNonQueryAsync(
            $"INSERT INTO [{tableName}] (Value) VALUES (@value)",
            transaction: null,
            configureParameters: parameters => parameters.AddWithValue("@value", "hello"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, rowsAffected);
    }

    [Fact]
    public async Task ExecuteReaderAsync_MapsTheResultViaTheCallback()
    {
        await using SqlConnection connection = await OpenConnectionAsync();

        string? result = await connection.ExecuteReaderAsync(
            "SELECT NULL, 'x'",
            transaction: null,
            configureParameters: null,
            readResult: async (reader, ct) =>
            {
                Assert.True(await reader.ReadAsync(ct));
                return reader.GetNullableString(0);
            },
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteReaderAsync_NonNullColumn_GetNullableStringReturnsTheValue()
    {
        await using SqlConnection connection = await OpenConnectionAsync();

        string? result = await connection.ExecuteReaderAsync(
            "SELECT NULL, 'x'",
            transaction: null,
            configureParameters: null,
            readResult: async (reader, ct) =>
            {
                Assert.True(await reader.ReadAsync(ct));
                return reader.GetNullableString(1);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("x", result);
    }

    private static async Task<SqlConnection> OpenConnectionAsync()
    {
        SqlConnection connection = new(TestSqlConnectionString.RequireOrSkip());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }
}
