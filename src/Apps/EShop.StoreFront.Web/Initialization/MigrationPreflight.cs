// <copyright file="MigrationPreflight.cs" company="Henrik Jensen">
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

namespace Hj.EShop.StoreFront.Web.Initialization;

internal static class MigrationPreflight
{
    public static async Task WaitForBothMigrationsAsync(IConfiguration configuration, ILogger logger, CancellationToken cancellationToken)
    {
        string cmsConnectionString = configuration.GetConnectionString("EPiServerDB")
            ?? throw new InvalidOperationException("Missing connection string 'EPiServerDB'.");
        string commerceConnectionString = configuration.GetConnectionString("EcfSqlConnection")
            ?? throw new InvalidOperationException("Missing connection string 'EcfSqlConnection'.");

        string cmsExpectedVersion = OptimizelyInstalledVersion.Get("EPiServer.Data");
        string commerceExpectedVersion = OptimizelyInstalledVersion.Get("Mediachase.Commerce");

        await MigrationReadinessWaiter.WaitForMigrationAsync(
            cmsConnectionString, MigrationNames.StorefrontCmsComponent, cmsExpectedVersion, logger, cancellationToken);
        await MigrationReadinessWaiter.WaitForMigrationAsync(
            commerceConnectionString, MigrationNames.StorefrontCommerceComponent, commerceExpectedVersion, logger, cancellationToken);
    }
}
