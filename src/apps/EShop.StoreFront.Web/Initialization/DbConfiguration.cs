// <copyright file="DbConfiguration.cs" company="Henrik Jensen">
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


using EPiServer.Data;
using EPiServer.Data.SchemaUpdates;

namespace Hj.EShop.StoreFront.Web.Initialization;

internal static class DbConfiguration
{
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        // See doc/adr/0023-explicit-database-schema-migration-resources.md - normal
        // startup must never mutate schema.
        services.Configure<DataAccessOptions>(options =>
        {
            options.UpdateDatabaseSchema = false;
            options.CreateDatabaseSchema = false;
        });
        services.AddSingleton<ISchemaValidator, NoAutomaticSchemaUpdateValidator>();

        return services;
    }

    internal sealed class NoAutomaticSchemaUpdateValidator : ISchemaValidator
    {
        public bool IsDatabaseUpdateAllowed(ConnectionStringOptions connectionStringOptions)
        {
            return false;
        }

        public void BeforeUpdating(ConnectionStringOptions connectionStringOptions)
        {
        }
    }
}
