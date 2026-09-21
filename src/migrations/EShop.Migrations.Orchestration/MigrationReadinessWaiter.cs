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

// See doc/CHRONICLE.md — needed for hosts that can't degrade to "start anyway, report Unhealthy" (e.g. Optimizely's DatabaseSchemaHost); Aspire's .WaitFor has no effect once deployed.
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

    // A cold-starting SQL Server, or a database that doesn't exist yet, must report "not migrated" and let the caller retry, not crash the whole wait.
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
                LogLastAttempt(logger, component, marker.FailureMessage);
                return false;
            }

            if (marker.SchemaVersion != expectedVersion)
            {
                LogBuildNotMatching(logger, marker.SchemaVersion, component, expectedVersion);
                return false;
            }

            return true;
        }
        catch (SqlException ex)
        {
            LogCouldNotCheck(logger, component, ex);
            return false;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Migration for '{Component}' has completed.")]
    private static partial void LogMigrationCompleted(ILogger logger, string component);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Waiting for migration '{Component}' to complete (attempt {Attempt}/{MaxAttempts})...")]
    private static partial void LogWaitingForMigration(ILogger logger, string component, int attempt, int maxAttempts);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Last migration attempt for '{Component}' failed: {FailureMessage}")]
    private static partial void LogLastAttempt(ILogger logger, string component, string? failureMessage);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Schema marker version '{ActualVersion}' for '{Component}' does not match this build's expected version '{ExpectedVersion}'.")]
    private static partial void LogBuildNotMatching(ILogger logger, string actualVersion, string component, string expectedVersion);

    [LoggerMessage(
        LogLevel.Warning,
        Message = "Could not check migration status for '{Component}' yet.")]
    private static partial void LogCouldNotCheck(ILogger logger, string component, Exception exception);
}
