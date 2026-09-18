// <copyright file="CommerceHealthCheck.cs" company="Henrik Jensen">
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
using EPiServer.Commerce.Catalog.ContentTypes;
using EPiServer.Core;
using Mediachase.Commerce.Catalog;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hj.EShop.StoreFront.Web.Features.HealthChecks;

internal sealed class CommerceHealthCheck : IHealthCheck
{
    private readonly IContentRepository _contentRepository;
    private readonly ReferenceConverter _referenceConverter;

    public CommerceHealthCheck(IContentRepository contentRepository, ReferenceConverter referenceConverter)
    {
        _contentRepository = contentRepository;
        _referenceConverter = referenceConverter;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = HealthCheckResult.Healthy("Commerce is healthy");

#pragma warning disable CA1031
        try
        {
            var lo = new LoaderOptions { LanguageLoaderOption.MasterLanguage() };
            IEnumerable<CatalogContent> catalogs = _contentRepository.GetChildren<CatalogContent>(_referenceConverter.GetRootLink(), lo);
            foreach (CatalogContent catalog in catalogs)
            {
                IEnumerable<IContent>? children = _contentRepository.GetChildren<IContent>(catalog.ContentLink, lo);
                if (children is null)
                {
                    result = HealthCheckResult.Degraded($"Commerce is degraded: {catalog.Name}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            result = HealthCheckResult.Unhealthy("Commerce is down", ex);
        }
#pragma warning restore CA1031

        return Task.FromResult(result);
    }
}
