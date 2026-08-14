// <copyright file="MigrationReadinessWaiterTests.cs" company="Henrik Jensen">
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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hj.EShop.Migrations.Orchestration.Tests;

// Needs a real SQL Server - see TestSqlConnectionString. Excluded from the default
// `eshop test`/`dotnet test` run via --filter-not-trait "Category=Integration" (see
// UnitTestCommand in EShop.Cli), same as this repo's other Integration-tagged tests.
[Trait("Category", "Integration")]
public sealed class MigrationReadinessWaiterTests
{
    [Fact]
    public async Task WaitForMigrationAsync_MarkerAlreadySucceededWithExpectedVersion_ReturnsWithoutPolling()
    {
        string component = $"test-component-{Guid.NewGuid():N}";
        string connectionString = TestSqlConnectionString.RequireOrSkip();
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await SchemaMarkerStore.EnsureTableExistsAsync(connection, TestContext.Current.CancellationToken);
        await SchemaMarkerStore.WriteAsync(
            connection,
            new SchemaMarker(component, "1.0.0", DateTime.UtcNow, Succeeded: true, FailureMessage: null),
            TestContext.Current.CancellationToken);

        // A single attempt with no delay is enough to prove it returns on the first
        // check, rather than exhausting maxAttempts.
        await MigrationReadinessWaiter.WaitForMigrationAsync(
            connectionString, component, "1.0.0", NullLogger.Instance, TestContext.Current.CancellationToken, maxAttempts: 1);
    }

    [Fact]
    public async Task WaitForMigrationAsync_NoMarkerEverWritten_ThrowsAfterAttemptsExhausted()
    {
        string component = $"test-component-{Guid.NewGuid():N}";
        string connectionString = TestSqlConnectionString.RequireOrSkip();

        await Assert.ThrowsAsync<InvalidOperationException>(() => MigrationReadinessWaiter.WaitForMigrationAsync(
            connectionString,
            component,
            "1.0.0",
            NullLogger.Instance,
            TestContext.Current.CancellationToken,
            maxAttempts: 2,
            pollDelay: TimeSpan.Zero));
    }

    [Fact]
    public async Task WaitForMigrationAsync_MarkerVersionDoesNotMatchExpected_ThrowsAfterAttemptsExhausted()
    {
        string component = $"test-component-{Guid.NewGuid():N}";
        string connectionString = TestSqlConnectionString.RequireOrSkip();
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await SchemaMarkerStore.EnsureTableExistsAsync(connection, TestContext.Current.CancellationToken);
        await SchemaMarkerStore.WriteAsync(
            connection,
            new SchemaMarker(component, "1.0.0", DateTime.UtcNow, Succeeded: true, FailureMessage: null),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => MigrationReadinessWaiter.WaitForMigrationAsync(
            connectionString,
            component,
            "2.0.0",
            NullLogger.Instance,
            TestContext.Current.CancellationToken,
            maxAttempts: 2,
            pollDelay: TimeSpan.Zero));
    }

    [Fact]
    public async Task WaitForMigrationAsync_LastAttemptFailed_ThrowsAfterAttemptsExhausted()
    {
        string component = $"test-component-{Guid.NewGuid():N}";
        string connectionString = TestSqlConnectionString.RequireOrSkip();
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await SchemaMarkerStore.EnsureTableExistsAsync(connection, TestContext.Current.CancellationToken);
        await SchemaMarkerStore.WriteAsync(
            connection,
            new SchemaMarker(component, "1.0.0", DateTime.UtcNow, Succeeded: false, "boom"),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => MigrationReadinessWaiter.WaitForMigrationAsync(
            connectionString,
            component,
            "1.0.0",
            NullLogger.Instance,
            TestContext.Current.CancellationToken,
            maxAttempts: 2,
            pollDelay: TimeSpan.Zero));
    }
}
