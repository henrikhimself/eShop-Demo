// <copyright file="SiteInitialization.cs" company="Henrik Jensen">
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

using EPiServer;
using EPiServer.Applications;
using EPiServer.Commerce.Initialization;
using EPiServer.Commerce.Routing;
using EPiServer.Core;
using EPiServer.DataAccess;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.Security;
using Hj.EShop.Common;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms;
using Hj.EShop.StoreFront.Web.Foundation.Options;
using Microsoft.Extensions.Options;

namespace Hj.EShop.StoreFront.Web.Initialization;

[InitializableModule]
[ModuleDependency(typeof(InitializationModule))]
internal sealed class SiteInitialization : IInitializableModule
{
    public void Initialize(InitializationEngine context)
    {
        context.InitComplete += InitCompleteAsync;
    }

    public void Uninitialize(InitializationEngine context)
    {
        context.InitComplete -= InitCompleteAsync;
    }

    private static async Task<IRoutableApplication> EnsureDefaultApplicationAsync(IServiceProvider serviceProvider)
    {
        IApplicationRepository applicationRepository = serviceProvider.GetRequiredService<IApplicationRepository>();
        IContentRepository contentRepository = serviceProvider.GetRequiredService<IContentRepository>();
        IOptions<StoreFrontOptions> storeFrontOptions = serviceProvider.GetRequiredService<IOptions<StoreFrontOptions>>();

        IRoutableApplication? defaultApp = await applicationRepository.GetDefaultAsync();

        if (defaultApp is null)
        {
            FrontPage frontPage = contentRepository.GetDefault<FrontPage>(ContentReference.RootPage);
            frontPage.Name = "FrontPage";
            ContentReference frontPageReference = contentRepository.Save(frontPage, SaveAction.Publish, AccessLevel.NoAccess);

            DefaultOptions? appDefaults = storeFrontOptions.Value.Defaults;
            string appName = appDefaults?.ApplicationName ?? "Default" + Random.Shared.Next();
            string hostAuthority = appDefaults?.ApplicationAuthority
                ?? $"{KnownNames.ReverseProxyStorefrontHostName}:{KnownNames.ReverseProxyHttpsPort}";

            var storeFrontApp = new InProcessWebsite(appName.ToLowerInvariant(), frontPageReference)
            {
                DisplayName = appName
            };
            var host = new ApplicationHost(hostAuthority)
            {
                Type = ApplicationHostType.Default,
                PreferredUrlScheme = UrlScheme.Http,
            };
            storeFrontApp.Hosts.Add(host);
            await applicationRepository.SaveAsync(storeFrontApp, CancellationToken.None);

            defaultApp = storeFrontApp;
            await applicationRepository.MakeDefaultAsync(defaultApp, true, CancellationToken.None);
        }

        return defaultApp;
    }

    private static void MapCatalogRoute(IRoutableApplication defaultApp)
    {
        bool enableOutgoingSeoUri = false;
        CatalogRouteHelper.MapDefaultHierarchialRouter(() => ContentReference.IsNullOrEmpty(ContentReference.StartPage)
            ? ContentReference.RootPage
            : defaultApp.EntryPoint, enableOutgoingSeoUri);
    }

    private async void InitCompleteAsync(object? sender, EventArgs args)
    {
        if (sender is not InitializationEngine context)
        {
            return;
        }

        await using AsyncServiceScope scope = context.Services.CreateAsyncScope();
        IServiceProvider serviceProvider = scope.ServiceProvider;

        IRoutableApplication defaultApp = await EnsureDefaultApplicationAsync(serviceProvider);
        MapCatalogRoute(defaultApp);
    }
}
