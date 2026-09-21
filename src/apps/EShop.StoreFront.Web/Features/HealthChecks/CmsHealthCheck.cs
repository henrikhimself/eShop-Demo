// <copyright file="CmsHealthCheck.cs" company="Henrik Jensen">
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

using EPiServer.Applications;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hj.EShop.StoreFront.Web.Features.HealthChecks;

internal sealed class CmsHealthCheck : IHealthCheck
{
    private readonly IApplicationRepository _applicationRepository;

    public CmsHealthCheck(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = HealthCheckResult.Healthy("CMS is healthy");

#pragma warning disable CA1031
        try
        {
            IEnumerable<Application> apps = await _applicationRepository.ListAsync(cancellationToken);
            if (!apps.Any())
            {
                result = HealthCheckResult.Degraded($"CMS is degraded");
            }
        }
        catch (Exception ex)
        {
            result = HealthCheckResult.Unhealthy("CMS is down", ex);
        }
#pragma warning restore CA1031

        return result;
    }
}
