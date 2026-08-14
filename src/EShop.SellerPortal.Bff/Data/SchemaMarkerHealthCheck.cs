// <copyright file="SchemaMarkerHealthCheck.cs" company="Henrik Jensen">
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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hj.EShop.SellerPortal.Bff.Data;

// The Bff no longer migrates its own database (see doc/CHRONICLE.md - a dedicated
// EShop.SellerPortal.MigrationRunner does, lock-protected) - this is what makes the Bff
// report not-ready until that has actually happened, instead of starting to serve
// requests against a schema that may not exist yet. Not tagged "live": liveness must
// stay independent of schema readiness.
internal sealed class SchemaMarkerHealthCheck(SellerPortalDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // A health check must never throw - it must always resolve to a result. A
        // cold-starting SQL Server can reset this connection attempt (the same
        // pre-login handshake failure the migration runner's own retry guards
        // against); there's no need to retry it here too, since the health endpoint is
        // itself polled repeatedly - a quick Unhealthy now, re-checked shortly after,
        // serves the same purpose without blocking the response.
        try
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }
        catch (SqlException)
        {
            return HealthCheckResult.Unhealthy("Could not connect to the database yet.");
        }

        try
        {
            var connection = (SqlConnection)dbContext.Database.GetDbConnection();

            SchemaMarker? marker;
            try
            {
                marker = await SchemaMarkerStore.GetLatestAsync(
                    connection, MigrationNames.SellerPortalComponent, cancellationToken);
            }
            catch (SqlException)
            {
                // The marker table itself doesn't exist yet - the migration runner has
                // never completed a run against this database.
                return HealthCheckResult.Unhealthy("No schema migration marker table found yet.");
            }

            if (marker is null)
            {
                return HealthCheckResult.Unhealthy("No schema migration marker found yet.");
            }

            if (!marker.Succeeded)
            {
                return HealthCheckResult.Unhealthy($"Last schema migration attempt failed: {marker.FailureMessage}");
            }

            string expectedVersion = dbContext.Database.GetMigrations().LastOrDefault() ?? string.Empty;
            return marker.SchemaVersion == expectedVersion
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(
                    $"Schema marker version '{marker.SchemaVersion}' does not match this build's expected version '{expectedVersion}'.");
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
