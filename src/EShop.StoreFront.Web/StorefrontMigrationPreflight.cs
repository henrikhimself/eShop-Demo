// <copyright file="StorefrontMigrationPreflight.cs" company="Henrik Jensen">
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
using Hj.EShop.Migrations.Optimizely;
using Hj.EShop.Migrations.Orchestration;

namespace Hj.EShop.StoreFront.Web;

// Optimizely's own DatabaseSchemaHost hosted service crashes the whole app at startup
// if the schema doesn't exist yet - there is no EF-Core-style "start anyway, report
// Unhealthy" option (see doc/CHRONICLE.md). AppHost-level ordering (.WaitFor) was
// rejected too: it has no effect once deployed to Azure Container Apps, so relying on it
// would make local behavior diverge from production instead of matching it.
// MigrationReadinessWaiter (EShop.Migrations.Orchestration) checks the same
// SchemaMigrationMarkers table EShop.StoreFront.MigrationRunner writes to, before ever
// building the real CMS/Commerce host, so DatabaseSchemaHost never has anything to crash
// on - pure in-process C#, so it behaves identically regardless of orchestrator.
internal static class StorefrontMigrationPreflight
{
    public static async Task WaitForBothMigrationsAsync(IConfiguration configuration, ILogger logger, CancellationToken cancellationToken)
    {
        // Same connection strings AddCms()/AddCommerce() will use for real once the
        // host actually starts - not a separate reference, so no extra AppHost wiring
        // is needed purely for this check.
        string cmsConnectionString = configuration.GetConnectionString("EPiServerDB")
            ?? throw new InvalidOperationException("Missing connection string 'EPiServerDB'.");
        string commerceConnectionString = configuration.GetConnectionString("EcfSqlConnection")
            ?? throw new InvalidOperationException("Missing connection string 'EcfSqlConnection'.");

        // Read from the actual referenced assemblies, not duplicated as configuration on
        // top of the Directory.Packages.props pin - see OptimizelyInstalledVersion.
        string cmsExpectedVersion = OptimizelyInstalledVersion.Get("EPiServer.Data");
        string commerceExpectedVersion = OptimizelyInstalledVersion.Get("Mediachase.Commerce");

        await MigrationReadinessWaiter.WaitForMigrationAsync(
            cmsConnectionString, MigrationNames.StorefrontCmsComponent, cmsExpectedVersion, logger, cancellationToken);
        await MigrationReadinessWaiter.WaitForMigrationAsync(
            commerceConnectionString, MigrationNames.StorefrontCommerceComponent, commerceExpectedVersion, logger, cancellationToken);
    }
}
