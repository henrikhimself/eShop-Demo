// <copyright file="DefaultContentService.cs" company="Henrik Jensen">
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
using EPiServer.Commerce.Catalog.ContentTypes;
using EPiServer.Commerce.SpecializedProperties;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAccess;
using EPiServer.Security;
using Hj.EShop.StoreFront.Web.Features.DefaultSite;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Commerce;
using Hj.EShop.StoreFront.Web.Foundation.Operations;
using Hj.EShop.StoreFront.Web.Foundation.SiteSettings;
using Mediachase.Commerce.Catalog;

namespace Hj.EShop.StoreFront.Web.Features.DefaultContent.Internal;

internal sealed class DefaultContentService : IDefaultContentService
{
    private readonly ReferenceConverter _referenceConverter;
    private readonly IContentRepository _contentRepository;
    private readonly IContentTypeRepository _contentTypeRepository;

    public DefaultContentService(
        ReferenceConverter referenceConverter,
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository)
    {
        _referenceConverter = referenceConverter;
        _contentRepository = contentRepository;
        _contentTypeRepository = contentTypeRepository;
    }

    public async Task<OperationResponse> CreateCatalog(OperationDataRequest<DefaultSiteDataDetail> request)
    {
        DefaultSiteDataDetail defaultSite = request.Data;
        ItemCollection<string> catalogLanguages = [.. defaultSite.Languages.Select(x => x.Name).ToList()];

        ContentReference rootReference = _referenceConverter.GetRootLink();

        if (_contentRepository.GetChildren<CatalogContent>(rootReference).Any(x => x.IsPrimary))
        {
            return request.Ok();
        }

        MovieCatalog catalog = _contentRepository.GetDefault<MovieCatalog>(rootReference);
        catalog.Name = "Movies";
        catalog.IsPrimary = true;
        catalog.DefaultCurrency = "EUR";
        catalog.DefaultLanguage = defaultSite.MainLanguage.Name;
        catalog.CatalogLanguages = catalogLanguages;
        catalog.WeightBase = "kgs";
        catalog.LengthBase = "cm";

        ContentReference catalogReference = _contentRepository.Save(catalog, SaveAction.Publish, AccessLevel.NoAccess);
        if (ContentReference.IsNullOrEmpty(catalogReference))
        {
            return request.Fail("Failed to create movie catalog");
        }

        return request.Ok();
    }

    public async Task<OperationResponse> CreateSiteSettings(OperationDataRequest<DefaultSiteDataDetail> request)
    {
        DefaultSiteDataDetail defaultSite = request.Data;
        ContentReference entryPoint = defaultSite.EntryPoint;
        CultureInfo language = defaultSite.MainLanguage;

        ISiteSettingsPage referencingPage = _contentRepository.Get<ISiteSettingsPage>(entryPoint, language);
        if (referencingPage.SiteSettings is not null)
        {
            return request.Ok();
        }

        SiteSettingsPage siteSettingsPage = _contentRepository.GetDefault<SiteSettingsPage>(entryPoint, language);
        siteSettingsPage.Name = "Site Settings";
        ContentReference siteSettingsReference = _contentRepository.Save(siteSettingsPage, SaveAction.Publish, AccessLevel.NoAccess);

        Type[] settingsBlockTypes = [
            typeof(LayoutSettingsBlock),
            typeof(ServiceMenuSettingsBlock)
        ];

        siteSettingsPage.SiteSettings = new();
        foreach (Type settingsBlockType in settingsBlockTypes)
        {
            ContentType? settingsBlockContentType = _contentTypeRepository.Load(settingsBlockType);
            if (settingsBlockContentType is null || Activator.CreateInstance(settingsBlockType) is not BlockData settingsBlock)
            {
                continue;
            }

            settingsBlock.ContentTypeID = settingsBlockContentType.ID;
            ContentAreaItem contentAreaItem = new()
            {
                InlineBlock = settingsBlock,
            };
            siteSettingsPage.SiteSettings.Items.Add(contentAreaItem);
        }

        if (_contentRepository.Save(siteSettingsPage, SaveAction.Publish, AccessLevel.NoAccess) is null)
        {
            return request.Fail("Could not save and publish site settings page");
        }

        var writableReferencingPage = (ISiteSettingsPage)referencingPage.CreateWritableClone();
        writableReferencingPage.SiteSettings = siteSettingsReference;
        if (_contentRepository.Save(writableReferencingPage, SaveAction.Publish, AccessLevel.NoAccess) is null)
        {
            return request.Fail("Could not save and publish site settings referencing page");
        }

        return request.Ok();
    }
}
