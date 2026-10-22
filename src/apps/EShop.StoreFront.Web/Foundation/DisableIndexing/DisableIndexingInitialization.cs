// <copyright file="DisableIndexingInitialization.cs" company="Henrik Jensen">
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

using EPiServer.Cms.Shell;
using EPiServer.DependencyInjection;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using Hj.EShop.StoreFront.Web.Foundation.DisableIndexing.Internal;

namespace Hj.EShop.StoreFront.Web.Foundation.DisableIndexing;

[InitializableModule]
[ModuleDependency(typeof(InitializableModule))]
internal sealed class DisableIndexingInitialization : IConfigurableModule
{
    public void ConfigureContainer(ServiceConfigurationContext context)
    {
        context.Services.AddCmsValidator<ValidateDisableIndexing>();
    }

    public void Initialize(InitializationEngine context)
    {
        using IServiceScope scope = context.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDisableIndexingService>()
            .Initialize();
    }

    public void Uninitialize(InitializationEngine context)
    {
        using IServiceScope scope = context.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDisableIndexingService>()
            .Uninitialize();
    }
}
