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
using Hj.EShop.StoreFront.Web.Foundation.Options;
using Hj.EShop.StoreFront.Web.Foundation.Presentation;
using Hj.EShop.StoreFront.Web.Initialization;
using Mediachase.Commerce.Anonymous;
using Microsoft.AspNetCore.HttpOverrides;

namespace Hj.EShop.StoreFront.Web;

internal sealed class Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
{
    public void ConfigureServices(IServiceCollection services)
    {
        Extensions.AddServiceDefaults(services, configuration, "EShop.StoreFront.Web", webHostEnvironment);

        services.AddOptions<StoreFrontOptions>().BindConfiguration("StoreFront");

        services.AddHybridCache();

        services
            .AddMvc(o =>
            {
                o.Conventions.Add(new FeaturesControllerModelConvention());
            })
            .AddRazorOptions(o => o.ViewLocationExpanders.Add(new FeaturesViewLocationExpander()));

        services
            .AddCms()
            .AddCommerce()
            .AddLanguageManager()
            .AddForms()
            .AddVisitorGroupsMvc()
            .AddVisitorGroupsUI();

        services.AddAuthConfiguration(configuration, webHostEnvironment);

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        });

        services
            .AddEmbeddedLocalization<Startup>()
            .Configure<ProtectedModuleOptions>(o => o.RootPath = "~/ui") // match DXP path
            .Configure<UIOptions>(o => o.EditUrl = new Uri("~/ui/cms/", UriKind.Relative))
            .AddAzureBlobProvider(o =>
            {
                o.ConnectionString = configuration.GetConnectionString(KnownNames.ResourceStorageBlob);
                o.ContainerName = KnownNames.ResourceStorefrontStorageBlobContainer;
            });

        services.Configure<CookiePolicyOptions>(o =>
        {
            o.CheckConsentNeeded = context => false;
            o.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
            o.MinimumSameSitePolicy = SameSiteMode.None;
            o.Secure = CookieSecurePolicy.Always;
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

        app.UseEndpoints(o =>
        {
            o.MapDefaultEndpoints();
            o.MapContent();
        });
    }
}
