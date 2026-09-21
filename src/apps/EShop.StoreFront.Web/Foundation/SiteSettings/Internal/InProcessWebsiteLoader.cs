// <copyright file="InProcessWebsiteLoader.cs" company="Henrik Jensen">
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
using EPiServer.Core;
using EPiServer.Web;
using Hj.EShop.StoreFront.Web.Foundation.Operations;
using Hj.EShop.StoreFront.Web.Foundation.SiteSettings.Models;

namespace Hj.EShop.StoreFront.Web.Foundation.SiteSettings.Internal;

internal class InProcessWebsiteLoader : ISiteSettingsLoader
{
    private readonly IApplicationResolver _applicationResolver;
    private readonly IContentLoader _contentLoader;
    private readonly IContentAreaLoader _contentAreaLoader;

    public InProcessWebsiteLoader(
        IApplicationResolver applicationResolver,
        IContentLoader contentLoader,
        IContentAreaLoader contentAreaLoader)
    {
        _applicationResolver = applicationResolver;
        _contentLoader = contentLoader;
        _contentAreaLoader = contentAreaLoader;
    }

    public bool CanHandle(OperationDataRequest<GetSettingsRequest> request)
    {
        return request.Data.ContentLink is not null;
    }

    public async Task<OperationDataResponse<T>> LoadSettingsAsync<T>(OperationDataRequest<GetSettingsRequest> request)
        where T : class, ISiteSettingsBlock
    {
        Application? application = await _applicationResolver.GetByContentAsync(request.Data.ContentLink!, false, request.CancellationToken);
        if (application is not InProcessWebsite inProcessWebsite)
        {
            return request.Ok<T>();
        }

        ContentReference entryPoint = inProcessWebsite.EntryPoint;
        if (ContentReference.IsNullOrEmpty(entryPoint))
        {
            return request.Fail<T>("Application has no entrypoint");
        }

        try
        {
            ISiteSettingsPage page = _contentLoader.Get<ISiteSettingsPage>(
                inProcessWebsite.EntryPoint,
                [LanguageLoaderOption.FallbackWithMaster(request.Data.Language)]);

            var settings = (T?)page.SiteSettings?
                .Items
                .Select(item => _contentAreaLoader.LoadContent(item))
                .SingleOrDefault(item => item?.GetOriginalType() == typeof(T));

            return request.Ok(settings);
        }
        catch (ContentNotFoundException)
        {
            return request.Ok<T>();
        }
        catch (TypeMismatchException ex)
        {
            return request.Fail<T>(ex);
        }
    }
}
