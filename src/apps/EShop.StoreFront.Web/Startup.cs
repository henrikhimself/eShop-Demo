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

using EPiServer.Cms.UI.VisitorGroups;
using EPiServer.DependencyInjection;
using EPiServer.Shell.Modules;
using EPiServer.Web;
using EPiServer.Web.Routing;
using Hj.EShop.Common;
using Hj.EShop.ServiceDefaults;
using Hj.EShop.StoreFront.Web.Features.HealthChecks;
using Hj.EShop.StoreFront.Web.Initialization;
using Mediachase.Commerce.Anonymous;
using Microsoft.AspNetCore.HttpOverrides;

namespace Hj.EShop.StoreFront.Web;

internal sealed class Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
{
    public void ConfigureServices(IServiceCollection services)
    {
        Extensions.AddServiceDefaults(services, configuration, "EShop.StoreFront.Web", webHostEnvironment);

        // General-purpose infrastructure, not owned by any one feature - currently
        // backs HybridCacheTicketStore (AuthConfiguration.cs).
        services.AddHybridCache();

        services
            .AddCms()
            .AddCommerce()
            .AddLanguageManager()
            .AddForms()
            .AddVisitorGroupsMvc()
            .AddVisitorGroupsUI();

        services.AddAuthConfiguration(configuration, webHostEnvironment);

        // Keep the default KnownNetworks/KnownProxies (loopback only) instead of clearing
        // them - an empty list would let a client spoof X-Forwarded-Host, and the OIDC
        // handler computes redirect_uri from the forwarded host since every request
        // arrives via the reverse proxy.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        });

        services
            .AddEmbeddedLocalization<Startup>()
            .Configure<ProtectedModuleOptions>(p => p.RootPath = "~/ui") // match DXP path
            .Configure<UIOptions>(o => o.EditUrl = new Uri("~/ui/cms/", UriKind.Relative))
            .AddAzureBlobProvider(config =>
            {
                config.ConnectionString = configuration.GetConnectionString(KnownNames.ResourceStorageBlob);
                config.ContainerName = KnownNames.ResourceStorefrontStorageBlobContainer;
            });

        services.Configure<CookiePolicyOptions>(options =>
        {
            options.CheckConsentNeeded = context => false;
            options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
            options.MinimumSameSitePolicy = SameSiteMode.None;
            options.Secure = CookieSecurePolicy.Always;
        });

        services.AddDatabase();
        services.AddScrutorScan();
        services
            .AddHealthChecks()
            .AddCheck<CmsHealthCheck>(nameof(CmsHealthCheck))
            .AddCheck<CommerceHealthCheck>(nameof(CommerceHealthCheck));
    }

    public static void Configure(IApplicationBuilder app)
    {
        app.UseAnonymousId();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseForwardedHeaders();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseDefaultEndpointsMiddleware();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDefaultEndpoints();
            endpoints.MapContent();
        });
    }
}
