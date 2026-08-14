// <copyright file="SchemaMigrationLockTests.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Migrations.Orchestration.Tests;

// Needs a real SQL Server - see TestSqlConnectionString. Excluded from the default
// `eshop test`/`dotnet test` run via --filter-not-trait "Category=Integration" (see
// UnitTestCommand in EShop.Cli), same as this repo's other Integration-tagged tests.
[Trait("Category", "Integration")]
public sealed class SchemaMigrationLockTests
{
    [Fact]
    public async Task TryAcquireAsync_LockNotHeld_ReturnsTrue()
    {
        string lockName = $"test-lock-{Guid.NewGuid():N}";
        await using SqlConnection connection = await OpenConnectionAsync();

        bool acquired = await SchemaMigrationLock.TryAcquireAsync(
            connection, lockName, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(acquired);
    }

    // Proves MIGRATION-PLAN.md's "Test concurrent migration runner starts": two
    // connections racing the same lock name - only one can hold it at a time.
    [Fact]
    public async Task TryAcquireAsync_AlreadyHeldByAnotherConnection_TimesOutAndReturnsFalse()
    {
        string lockName = $"test-lock-{Guid.NewGuid():N}";
        await using SqlConnection holder = await OpenConnectionAsync();
        await using SqlConnection contender = await OpenConnectionAsync();
        bool holderAcquired = await SchemaMigrationLock.TryAcquireAsync(
            holder, lockName, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(holderAcquired);

        bool contenderAcquired = await SchemaMigrationLock.TryAcquireAsync(
            contender, lockName, TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        Assert.False(contenderAcquired);

        await SchemaMigrationLock.ReleaseAsync(holder, lockName, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ReleaseAsync_ThenAnotherConnectionAcquires_Succeeds()
    {
        string lockName = $"test-lock-{Guid.NewGuid():N}";
        await using SqlConnection first = await OpenConnectionAsync();
        await using SqlConnection second = await OpenConnectionAsync();
        Assert.True(await SchemaMigrationLock.TryAcquireAsync(
            first, lockName, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        await SchemaMigrationLock.ReleaseAsync(first, lockName, TestContext.Current.CancellationToken);
        bool secondAcquired = await SchemaMigrationLock.TryAcquireAsync(
            second, lockName, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(secondAcquired);
        await SchemaMigrationLock.ReleaseAsync(second, lockName, TestContext.Current.CancellationToken);
    }

    private static async Task<SqlConnection> OpenConnectionAsync()
    {
        SqlConnection connection = new(TestSqlConnectionString.RequireOrSkip());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }
}
