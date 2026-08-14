// <copyright file="SchemaMigrator.cs" company="Henrik Jensen">
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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Hj.EShop.SellerPortal.MigrationRunner;

// Pulled out of Program.cs (not public - SellerPortalDbContext itself is internal, only
// reachable here via InternalsVisibleTo) so a test can drive this against a real SQL
// Server without needing a full Host - same reasoning as ServiceBusQueueConsumer's
// public HandleMessageAsync in EShop.Messaging. Named distinctly from the project/
// namespace (EShop.SellerPortal.MigrationRunner) to avoid a type name that shadows its
// own containing namespace segment.
internal static partial class SchemaMigrator
{
    public static async Task<int> RunAsync(SellerPortalDbContext dbContext, ILogger logger, CancellationToken cancellationToken)
    {
        // A cold-starting SQL Server can reset this very first connection attempt (the
        // same pre-login handshake failure MigrateAsync's own retry below guards
        // against) - unwrapped, that crashes the whole runner before it ever reaches
        // that retry logic.
        await SqlExceptionRetry.RunAsync(
            () => dbContext.Database.OpenConnectionAsync(cancellationToken), maxAttempts: 5, logger);
        try
        {
            // sp_getapplock's LockOwner=Session ties the lock to this exact
            // connection's SQL Server session - the migration below must run on this
            // same open connection (EF reuses it as-is once already open) or the lock
            // protects nothing.
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();

            bool acquired = await SchemaMigrationLock.TryAcquireAsync(
                connection, MigrationNames.SellerPortalLockName, TimeSpan.FromMinutes(5), cancellationToken);
            if (!acquired)
            {
                logger.LogError("Could not acquire the migration lock within the timeout - another runner may be stuck.");
                return 1;
            }

            try
            {
                return await MigrateAsync(dbContext, connection, logger, cancellationToken);
            }
            finally
            {
                await SchemaMigrationLock.ReleaseAsync(connection, MigrationNames.SellerPortalLockName, cancellationToken);
            }
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task<int> MigrateAsync(
        SellerPortalDbContext dbContext, SqlConnection connection, ILogger logger, CancellationToken cancellationToken)
    {
        await SchemaMarkerStore.EnsureTableExistsAsync(connection, cancellationToken);

        string schemaVersion = dbContext.Database.GetMigrations().LastOrDefault() ?? string.Empty;
        try
        {
            IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
            await SqlExceptionRetry.RunAsync(
                () => strategy.ExecuteAsync(() => dbContext.Database.MigrateAsync(cancellationToken)),
                maxAttempts: 5,
                logger);

            await SchemaMarkerStore.WriteAsync(
                connection,
                new SchemaMarker(MigrationNames.SellerPortalComponent, schemaVersion, DateTime.UtcNow, Succeeded: true, FailureMessage: null),
                cancellationToken);

            LogMigrationSucceeded(logger, schemaVersion);
            return 0;
        }
#pragma warning disable CA1031 // Any failure here must still write a failure marker and exit non-zero, not crash unrecorded.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            await SchemaMarkerStore.WriteAsync(
                connection,
                new SchemaMarker(MigrationNames.SellerPortalComponent, schemaVersion, DateTime.UtcNow, Succeeded: false, ex.Message),
                cancellationToken);

            logger.LogError(ex, "Migration failed.");
            return 1;
        }
    }

    // LoggerMessage source-generated method: the analyzer can prove this never
    // evaluates/formats its argument unless the log level is actually enabled, unlike a
    // plain logger.LogInformation(...) call - the proper fix for CA1873, not a suppression.
    [LoggerMessage(Level = LogLevel.Information, Message = "Migration succeeded; schema is now at '{SchemaVersion}'.")]
    private static partial void LogMigrationSucceeded(ILogger logger, string schemaVersion);
}
