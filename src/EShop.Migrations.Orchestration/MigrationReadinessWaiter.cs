// <copyright file="MigrationReadinessWaiter.cs" company="Henrik Jensen">
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
using Microsoft.Extensions.Logging;

namespace Hj.EShop.Migrations.Orchestration;

// Extracted from EShop.StoreFront.Web's StorefrontMigrationPreflight, which needed this
// because Optimizely's own DatabaseSchemaHost hosted service crashes the whole app at
// startup if the schema doesn't exist yet - there is no EF-Core-style "start anyway,
// report Unhealthy" option (see doc/CHRONICLE.md). AppHost-level ordering (.WaitFor) was
// rejected too: it has no effect once deployed to Azure Container Apps, so relying on it
// would make local behavior diverge from production instead of matching it. Any future
// project whose migration technology can't degrade to "start anyway, report Unhealthy"
// the way EF Core/SchemaMarkerHealthCheck can needs the same in-process wait - this is
// technology-agnostic (only depends on SchemaMarkerStore), so it lives alongside it
// rather than in any one component's own project.
public static partial class MigrationReadinessWaiter
{
    private const int DefaultMaxAttempts = 30;
    private static readonly TimeSpan _defaultPollDelay = TimeSpan.FromSeconds(10);

    public static async Task WaitForMigrationAsync(
        string connectionString,
        string component,
        string expectedVersion,
        ILogger logger,
        CancellationToken cancellationToken,
        int maxAttempts = DefaultMaxAttempts,
        TimeSpan? pollDelay = null)
    {
        TimeSpan delay = pollDelay ?? _defaultPollDelay;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (await IsMigratedAsync(connectionString, component, expectedVersion, logger, cancellationToken))
            {
                LogMigrationCompleted(logger, component);
                return;
            }

            LogWaitingForMigration(logger, component, attempt, maxAttempts);
            await Task.Delay(delay, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Migration for '{component}' did not complete within {maxAttempts} attempts ({maxAttempts * delay.TotalSeconds}s). "
            + "Check the migration runner's own logs.");
    }

    // A health check must never throw; this isn't one, but the same reasoning applies -
    // a cold-starting SQL Server, or a database that genuinely doesn't exist yet, must
    // report "not migrated" and let the caller retry, not crash the whole wait.
    private static async Task<bool> IsMigratedAsync(
        string connectionString, string component, string expectedVersion, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await using SqlConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken);
            SchemaMarker? marker = await SchemaMarkerStore.GetLatestAsync(connection, component, cancellationToken);

            if (marker is null)
            {
                return false;
            }

            if (!marker.Succeeded)
            {
                logger.LogWarning("Last migration attempt for '{Component}' failed: {FailureMessage}", component, marker.FailureMessage);
                return false;
            }

            // Mirrors SchemaMarkerHealthCheck (EShop.SellerPortal.Bff) - a marker from an
            // older build (a dependency version bump not yet re-migrated here) must not
            // be treated as "ready", the same way that check compares against the
            // current build's expected EF migration id.
            if (marker.SchemaVersion != expectedVersion)
            {
                logger.LogWarning(
                    "Schema marker version '{ActualVersion}' for '{Component}' does not match this build's expected version '{ExpectedVersion}'.",
                    marker.SchemaVersion,
                    component,
                    expectedVersion);
                return false;
            }

            return true;
        }
        catch (SqlException ex)
        {
            logger.LogWarning(ex, "Could not check migration status for '{Component}' yet.", component);
            return false;
        }
    }

    // LoggerMessage source-generated methods: the analyzer can prove these never
    // evaluate/format their arguments unless the log level is actually enabled, unlike a
    // plain logger.LogInformation(...) call - the proper fix for CA1873, not a suppression.
    [LoggerMessage(Level = LogLevel.Information, Message = "Migration for '{Component}' has completed.")]
    private static partial void LogMigrationCompleted(ILogger logger, string component);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Waiting for migration '{Component}' to complete (attempt {Attempt}/{MaxAttempts})...")]
    private static partial void LogWaitingForMigration(ILogger logger, string component, int attempt, int maxAttempts);
}
