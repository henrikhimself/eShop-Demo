// <copyright file="StorefrontSchemaMigrator.cs" company="Henrik Jensen">
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

using Hj.EShop.Migrations.Optimizely;
using Hj.EShop.Migrations.Orchestration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Hj.EShop.StoreFront.MigrationRunner;

// See doc/CHRONICLE.md (ADR 0023 follow-up) - this runner applies Optimizely's own SQL
// scripts instead of calling Database.MigrateAsync(), unlike the Seller Portal's runner.
internal static partial class StorefrontSchemaMigrator
{
    public static async Task<int> RunAsync(StorefrontMigrationOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = new(options.ConnectionString);

        // A cold-starting SQL Server can reset this first connection attempt; unwrapped,
        // that crashes the runner before it ever reaches the lock.
        await SqlExceptionRetry.RunAsync(() => connection.OpenAsync(cancellationToken), maxAttempts: 5, logger);
        try
        {
            bool acquired = await SchemaMigrationLock.TryAcquireAsync(
                connection, options.LockName, TimeSpan.FromMinutes(5), cancellationToken);
            if (!acquired)
            {
                logger.LogError("Could not acquire the migration lock within the timeout - another runner may be stuck.");
                return 1;
            }

            try
            {
                return await MigrateAsync(connection, options, logger, cancellationToken);
            }
            finally
            {
                await SchemaMigrationLock.ReleaseAsync(connection, options.LockName, cancellationToken);
            }
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<int> MigrateAsync(
        SqlConnection connection, StorefrontMigrationOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        await SchemaMarkerStore.EnsureTableExistsAsync(connection, cancellationToken);

        try
        {
            OptimizelyPackageScripts scripts = OptimizelyPackageScriptLocator.Locate(
                options.ToolsDirectory, options.BaselineScriptFileName, [.. options.IncrementalFolderNames]);

            List<FileInfo> files = [];
            if (scripts.BaselineScript is not null)
            {
                files.Add(scripts.BaselineScript);
            }

            files.AddRange(scripts.IncrementalScripts);

            // Retried like the connection-open above: a cold-starting SQL Server can also
            // reset a mid-batch connection during the transaction OptimizelySqlScriptRunner
            // wraps the whole batch in.
            await SqlExceptionRetry.RunAsync(
                () => OptimizelySqlScriptRunner.ExecuteAsync(connection, files, cancellationToken), maxAttempts: 5, logger);

            await SchemaMarkerStore.WriteAsync(
                connection,
                new SchemaMarker(options.ComponentName, options.TargetVersion, DateTime.UtcNow, Succeeded: true, FailureMessage: null),
                cancellationToken);

            LogMigrationSucceeded(logger, options.TargetVersion);
            return 0;
        }
#pragma warning disable CA1031 // Any failure here must still write a failure marker and exit non-zero, not crash unrecorded.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            await SchemaMarkerStore.WriteAsync(
                connection,
                new SchemaMarker(options.ComponentName, options.TargetVersion, DateTime.UtcNow, Succeeded: false, ex.Message),
                cancellationToken);

            logger.LogError(ex, "Migration failed.");
            return 1;
        }
    }

    // LoggerMessage avoids CA1873 properly: the analyzer can prove the argument is never
    // formatted unless the log level is enabled, unlike a plain logger.LogInformation(...) call.
    [LoggerMessage(Level = LogLevel.Information, Message = "Migration succeeded; schema is now at '{TargetVersion}'.")]
    private static partial void LogMigrationSucceeded(ILogger logger, string targetVersion);
}
