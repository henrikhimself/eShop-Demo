// <copyright file="DefaultSiteService.cs" company="Henrik Jensen">
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
using EPiServer;
using EPiServer.Applications;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAccess;
using EPiServer.Security;
using Hj.EShop.StoreFront.Web.Foundation.Extensions;
using Hj.EShop.StoreFront.Web.Foundation.Operations;

namespace Hj.EShop.StoreFront.Web.Features.DefaultSite.Internal;

internal sealed class DefaultSiteService : IDefaultSiteService
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IContentRepository _contentRepository;
    private readonly ContentRootRepository _contentRootRepository;

    public DefaultSiteService(
        IApplicationRepository applicationRepository,
        IContentRepository contentRepository,
        ContentRootRepository contentRootRepository)
    {
        _applicationRepository = applicationRepository;
        _contentRepository = contentRepository;
        _contentRootRepository = contentRootRepository;
    }

    public async Task<OperationDataResponse<DefaultSiteDataDetail>> GetDefaultSiteAsync(OperationRequest request)
    {
        IEnumerable<Application> applications = await _applicationRepository.ListAsync(request.CancellationToken);
        var defaultSite = (IRoutableApplication?)applications.FirstOrDefault(x => x is IRoutableApplication y && y.IsDefault);
        if (defaultSite is null)
        {
            return request.Ok<DefaultSiteDataDetail>(null);
        }

        ApplicationHost? defaultHost = defaultSite.Hosts.FirstOrDefault(x => x.Type == ApplicationHostType.Primary);
        if (defaultHost is null)
        {
            defaultHost = defaultSite.Hosts.FirstOrDefault(x => x.Type == ApplicationHostType.Default);
            if (defaultHost is null)
            {
                return request.Fail<DefaultSiteDataDetail>("Missing host on default application");
            }
        }

        if (!_contentRepository.TryGet(defaultSite.EntryPoint, out IContent startPage))
        {
            return request.Fail<DefaultSiteDataDetail>("Missing entry point for start page");
        }

        DefaultSiteDataDetail data = ToDefaultSiteDataDetail(defaultSite, defaultHost, startPage);
        return request.Ok(data);
    }

    public async Task<OperationDataResponse<DefaultSiteDataDetail>> CreateDefaultSiteAsync(OperationDataRequest<DefaultSiteData> request)
    {
        OperationDataResponse<DefaultSiteDataDetail> defaultSiteResponse = await GetDefaultSiteAsync(request);
        if (defaultSiteResponse.HasData)
        {
            return request.Fail<DefaultSiteDataDetail>("Default site exists");
        }

        ContentReference rootPage = _contentRootRepository.Load(SystemContentRootNames.RootPage);

        IContent? startPage = _contentRepository.GetDefault(request.Data.StartPageType, rootPage, request.Data.MainLanguage);
        if (startPage is null)
        {
            return request.Fail<DefaultSiteDataDetail>("Missing start page");
        }

        string appName = request.Data.Name;
        startPage.Name = appName;

        ContentReference startPageReference = _contentRepository.Save(startPage, SaveAction.Publish, AccessLevel.NoAccess);

        var websiteApp = new InProcessWebsite(appName.ToLowerInvariant(), startPageReference)
        {
            DisplayName = appName
        };
        var primaryHost = new ApplicationHost(request.Data.Authority)
        {
            Type = ApplicationHostType.Default,
            PreferredUrlScheme = request.Data.PreferredUrlScheme ?? UrlScheme.Https,
            Locale = request.Data.MainLanguage,
        };
        websiteApp.Hosts.Add(primaryHost);

        await _applicationRepository.SaveAsync(websiteApp, CancellationToken.None);

        IRoutableApplication defaultSite = websiteApp;
        await _applicationRepository.MakeDefaultAsync(defaultSite, true, CancellationToken.None);

        DefaultSiteDataDetail data = ToDefaultSiteDataDetail(defaultSite, primaryHost, startPage);
        return request.Ok(data);
    }

    private static DefaultSiteDataDetail ToDefaultSiteDataDetail(IRoutableApplication defaultSite, ApplicationHost defaultHost, IContent startPage)
    {
        CultureInfo mainLanguage = defaultHost.Locale ?? CultureInfo.GetCultureInfo("en");
        List<CultureInfo> languages = [.. ((ILocalizable)startPage).ExistingLanguages];

        DefaultSiteDataDetail data = new()
        {
            RoutableApplication = defaultSite,
            Name = startPage.Name,
            Authority = defaultHost.Authority,
            MainLanguage = mainLanguage,
            Languages = languages,
            StartPageType = startPage.GetOriginalType(),
            EntryPoint = startPage.ContentLink,
            PreferredUrlScheme = defaultHost.PreferredUrlScheme,
        };

        return data;
    }
}
