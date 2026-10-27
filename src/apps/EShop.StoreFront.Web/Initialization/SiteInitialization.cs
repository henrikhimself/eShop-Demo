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

using System.Globalization;
using EPiServer.Applications;
using EPiServer.Commerce.Initialization;
using EPiServer.Commerce.Routing;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.Security;
using Hj.EShop.Common;
using Hj.EShop.StoreFront.Web.Features.DefaultAccess;
using Hj.EShop.StoreFront.Web.Features.DefaultContent;
using Hj.EShop.StoreFront.Web.Features.DefaultSite;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms;
using Hj.EShop.StoreFront.Web.Foundation.Operations;
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

    private static async Task<DefaultSiteDataDetail> EnsureDefaultSiteAsync(
        OperationRequest request,
        IDefaultSiteService defaultSiteService,
        IOptions<SiteInitializationOptions> options)
    {
        OperationDataResponse<DefaultSiteDataDetail> response = await defaultSiteService.GetDefaultSiteAsync(request);
        if (response.HasError)
        {
            throw response.Error;
        }

        if (response.HasData)
        {
            return response.Data;
        }

        SiteInitializationOptions config = options.Value;
        string? applicationName = config?.ApplicationName;
        string? applicationAuthority = config?.ApplicationAuthority;
        string? applicationLanguage = config?.ApplicationLanguage;

        DefaultSiteData defaultSiteData = new()
        {
            Name = string.IsNullOrWhiteSpace(applicationName)
                ? "Default"
                : applicationName,
            Authority = string.IsNullOrWhiteSpace(applicationAuthority)
                ? $"{KnownNames.ReverseProxyStorefrontHostName}:{KnownNames.ReverseProxyHttpsPort}"
                : applicationAuthority,
            MainLanguage = CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(applicationLanguage)
                ? "en"
                : applicationLanguage),
            StartPageType = typeof(FrontPage),
            PreferredUrlScheme = UrlScheme.Http,
        };
        response = await defaultSiteService.CreateDefaultSiteAsync(request.CreateOperationRequest(defaultSiteData));

        if (response.HasError)
        {
            throw response.Error;
        }

        if (response.HasData)
        {
            return response.Data;
        }

        throw new InitializationException("Failed to ensure default application");
    }

    private static void EnsureDefaultAccess(
        OperationRequest request,
        DefaultSiteDataDetail defaultSite,
        IDefaultAccessService defaultAccessService)
    {
        defaultAccessService.ResetRootPage();
        defaultAccessService.ResetBluePrints();
        defaultAccessService.ResetWasteBasket();

        DefaultAccessData defaultAppAccess = new()
        {
            ContentReference = defaultSite.EntryPoint,
            Action = acl =>
            {
                if (!acl.IsInherited)
                {
                    acl.ToInherited();
                }
            },
            SaveType = SecuritySaveType.Replace,
        };
        defaultAccessService.Reset(request.CreateOperationRequest(defaultAppAccess));
    }

    private static async Task EnsureDefaultCatalogAsync(
        OperationRequest request,
        DefaultSiteDataDetail defaultSite,
        IDefaultContentService defaultContentService)
    {
        OperationDataRequest<DefaultSiteDataDetail> catalogRequest = request.CreateOperationRequest(defaultSite);
        await defaultContentService.CreateCatalog(catalogRequest);

        bool enableOutgoingSeoUri = false;
        CatalogRouteHelper.MapDefaultHierarchialRouter(() => defaultSite.EntryPoint, enableOutgoingSeoUri);
    }

    private static async Task EnsureSiteSettingsAsync(
        OperationRequest request,
        DefaultSiteDataDetail defaultSite,
        IDefaultContentService defaultContentService)
    {
        OperationDataRequest<DefaultSiteDataDetail> siteSettingsRequest = request.CreateOperationRequest(defaultSite);
        await defaultContentService.CreateSiteSettings(siteSettingsRequest);
    }

    private async void InitCompleteAsync(object? sender, EventArgs args)
    {
        if (sender is not InitializationEngine context)
        {
            return;
        }

        OperationRequest request = context.CreateOperationRequest();

        await using AsyncServiceScope scope = context.Services.CreateAsyncScope();
        IServiceProvider serviceProvider = scope.ServiceProvider;

        IDefaultSiteService defaultSiteService = serviceProvider.GetRequiredService<IDefaultSiteService>();
        IOptions<SiteInitializationOptions> options = serviceProvider.GetRequiredService<IOptions<SiteInitializationOptions>>();
        IDefaultAccessService defaultAccessService = serviceProvider.GetRequiredService<IDefaultAccessService>();
        IDefaultContentService defaultContentService = serviceProvider.GetRequiredService<IDefaultContentService>();

        DefaultSiteDataDetail defaultSite = await EnsureDefaultSiteAsync(
            request,
            defaultSiteService,
            options);

        EnsureDefaultAccess(
            request,
            defaultSite,
            defaultAccessService);

        await EnsureDefaultCatalogAsync(
            request,
            defaultSite,
            defaultContentService);

        await EnsureSiteSettingsAsync(
            request,
            defaultSite,
            defaultContentService);
    }
}
