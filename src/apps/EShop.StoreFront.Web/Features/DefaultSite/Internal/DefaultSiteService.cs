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
using EPiServer.DataAccess;
using EPiServer.Security;
using Hj.EShop.StoreFront.Web.Foundation.Operations;

namespace Hj.EShop.StoreFront.Web.Features.DefaultSite.Internal;

internal sealed class DefaultSiteService : IDefaultSiteService
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IContentRepository _contentRepository;

    public DefaultSiteService(
        IApplicationRepository applicationRepository,
        IContentRepository contentRepository)
    {
        _applicationRepository = applicationRepository;
        _contentRepository = contentRepository;
    }

    public async Task<OperationDataResponse<IRoutableApplication>> GetOrCreateAsync(OperationDataRequest<DefaultSiteData> request)
    {
        IRoutableApplication? defaultSite = await _applicationRepository.GetDefaultAsync();
        if (defaultSite is not null)
        {
            return request.Ok(defaultSite);
        }

        IContent? startPage = GetDefault(request.Data.StartPageType, ContentReference.RootPage, request.Data.MainLanguage);
        if (startPage is null)
        {
            return request.Fail<IRoutableApplication>("Missing start page");
        }

        string appName = request.Data.Name;
        startPage.Name = appName;
        _contentRepository.Save(startPage, SaveAction.Publish, AccessLevel.NoAccess);

        ContentReference startPageReference = startPage.ContentLink;
        var websiteApp = new InProcessWebsite(appName.ToLowerInvariant(), startPageReference)
        {
            DisplayName = appName
        };
        var host = new ApplicationHost(request.Data.Authority)
        {
            Type = ApplicationHostType.Default,
            PreferredUrlScheme = request.Data.PreferredUrlScheme ?? UrlScheme.Https,
            Locale = request.Data.MainLanguage,
        };
        websiteApp.Hosts.Add(host);

        await _applicationRepository.SaveAsync(websiteApp, CancellationToken.None);

        defaultSite = websiteApp;
        await _applicationRepository.MakeDefaultAsync(defaultSite, true, CancellationToken.None);

        return request.Ok(defaultSite);
    }

    private IContent? GetDefault(Type contentType, ContentReference parent, CultureInfo langauge)
    {
        return _contentRepository.GetType()
            .GetMethod(nameof(IContentRepository.GetDefault), [typeof(ContentReference), typeof(CultureInfo)])
            ?.MakeGenericMethod(contentType)
            ?.Invoke(_contentRepository, [parent, langauge]) as IContent;
    }
}
