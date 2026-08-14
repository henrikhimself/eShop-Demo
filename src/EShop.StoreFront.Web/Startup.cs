// <copyright file="Startup.cs" company="Henrik Jensen">
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
using EPiServer.DependencyInjection;
using EPiServer.Scheduler;
using EPiServer.Web.Routing;
using Hj.EShop.ServiceDefaults;
using Hj.EShop.StoreFront.Web.Authentication;
using Mediachase.Commerce.Anonymous;

namespace Hj.EShop.StoreFront.Web;

internal sealed class Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
{
    public void ConfigureServices(IServiceCollection services)
    {
        Extensions.AddServiceDefaults(services, configuration, "EShop.StoreFront.Web");

        // Keep the template's AddCms()/AddCommerce() path, but not AddCmsAspNetIdentity:
        // ADR 0002 requires external OIDC as the identity system of record.
        services
            .AddCms()
            .AddCommerce();

        services.AddAuthConfiguration(configuration, webHostEnvironment);

        // ADR 0023: never mutate schema from normal app startup; migration resources own
        // it. The validator below is a second guard if config drifts.
        services.Configure<DataAccessOptions>(options =>
        {
            options.UpdateDatabaseSchema = false;
            options.CreateDatabaseSchema = false;
        });
        services.AddSingleton<ISchemaValidator, NoAutomaticSchemaUpdateValidator>();

        if (webHostEnvironment.IsDevelopment())
        {
            // Keep template default: do not run scheduled jobs in Development.
            services.Configure<SchedulerOptions>(options => options.Enabled = false);
        }
    }

    public static void Configure(IApplicationBuilder app)
    {
        app.UseAnonymousId();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseDefaultEndpointsMiddleware();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDefaultEndpoints();
            endpoints.MapAuthEndpoints();
            endpoints.MapContent();
        });
    }
}

// Secondary safety net: block automatic schema updates even if config flags drift.
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
