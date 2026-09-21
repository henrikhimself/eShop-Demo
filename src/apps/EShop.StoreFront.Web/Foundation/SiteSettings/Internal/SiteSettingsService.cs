// <copyright file="SiteSettingsService.cs" company="Henrik Jensen">
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

using System.Reflection;
using EPiServer;
using EPiServer.Core;
using EPiServer.SpecializedProperties;
using Hj.EShop.StoreFront.Web.Foundation.Operations;
using Hj.EShop.StoreFront.Web.Foundation.SiteSettings.Models;

namespace Hj.EShop.StoreFront.Web.Foundation.SiteSettings.Internal;

internal class SiteSettingsService : ISiteSettingsService
{
    private readonly IContentRepository _contentRepository;
    private readonly IEnumerable<ISiteSettingsLoader> _siteSettingsLoaders;

    public SiteSettingsService(
        IContentRepository contentRepository,
        IEnumerable<ISiteSettingsLoader> siteSettingsLoaders)
    {
        _contentRepository = contentRepository;
        _siteSettingsLoaders = siteSettingsLoaders;
    }

    public async Task<OperationDataResponse<T>> GetSettingsAsync<T>(OperationDataRequest<GetSettingsRequest> request)
        where T : class, ISiteSettingsBlock
    {
        List<ISiteSettingsLoader> loaders = _siteSettingsLoaders.Where(impl => impl.CanHandle(request))?.ToList() ?? [];
        if (loaders.Count == 0)
        {
            return request.Fail<T>("No settings loader was found");
        }

        foreach (ISiteSettingsLoader loader in loaders)
        {
            OperationDataResponse<T> settings = await loader.LoadSettingsAsync<T>(request);
            if (!settings.IsSuccess || settings.HasData)
            {
                return settings;
            }
        }

        return request.Ok<T>();
    }

    public Task<OperationDataResponse<T>> GetDefaultAsync<T>(OperationDataRequest<GetDefaultRequest> request)
        where T : class, ISiteSettingsBlock, new()
    {
        T setting;

        try
        {
            ContentReference? parentLink = request.Data.ParentLink;
            setting = ContentReference.IsNullOrEmpty(parentLink)
                ? new T()
                : _contentRepository.GetDefault<T>(parentLink);
        }
        catch (AccessDeniedException)
        {
            return request.FailTask<T>("Access denied");
        }

        PropertyInfo[] propertyInfos = setting.GetOriginalType().GetProperties();
        foreach (PropertyInfo propertyInfo in propertyInfos)
        {
            Type propertyType = propertyInfo.PropertyType;

            if (propertyType == typeof(LinkItemCollection))
            {
                propertyInfo.SetValue(setting, new LinkItemCollection());
            }
            else if (propertyType == typeof(ContentArea))
            {
                propertyInfo.SetValue(setting, new ContentArea());
            }
        }

        return request.OkTask(setting);
    }
}
