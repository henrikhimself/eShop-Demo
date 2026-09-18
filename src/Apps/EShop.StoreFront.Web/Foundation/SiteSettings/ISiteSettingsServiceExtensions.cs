// <copyright file="ISiteSettingsServiceExtensions.cs" company="Henrik Jensen">
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

using Hj.EShop.StoreFront.Web.Foundation.Operations;
using Hj.EShop.StoreFront.Web.Foundation.SiteSettings.Models;

namespace Hj.EShop.StoreFront.Web.Foundation.SiteSettings;

internal static class ISiteSettingsServiceExtensions
{
    public static async Task<T> GetAsync<T>(this ISiteSettingsService i, OperationRequest request)
        where T : class, ISiteSettingsBlock, new()
    {
        return await i.GetAsync<T>(ToGetSettingsFromCmsRequest(request));
    }

    public static async Task<T> GetAsync<T>(this ISiteSettingsService i, OperationDataRequest<GetSettingsRequest> request)
        where T : class, ISiteSettingsBlock, new()
    {
        OperationDataResponse<T> settings = await i.GetSettingsAsync<T>(request);
        if (settings.IsSuccess && !settings.HasData)
        {
            settings = await i.GetDefaultAsync<T>(request);
        }

        if (settings.HasData)
        {
            return settings.Data;
        }

        throw new InvalidOperationException($"Missing settings for {typeof(T).Name}");
    }

    public static Task<OperationDataResponse<T>> GetSettingsAsync<T>(this ISiteSettingsService i, OperationRequest request)
        where T : class, ISiteSettingsBlock
    {
        return i.GetSettingsAsync<T>(ToGetSettingsFromCmsRequest(request));
    }

    public static Task<OperationDataResponse<T>> GetDefaultAsync<T>(this ISiteSettingsService i, OperationRequest request)
        where T : class, ISiteSettingsBlock, new()
    {
        return i.GetDefaultAsync<T>(request.CreateOperationRequest<GetDefaultRequest>(new()));
    }

    private static OperationDataRequest<GetSettingsRequest> ToGetSettingsFromCmsRequest(OperationRequest request)
    {
        return request.CreateOperationRequest<GetSettingsRequest>(new()
        {
            ContentLink = request.Context.ContentLink,
            Language = request.Context.Language,
        });
    }
}
