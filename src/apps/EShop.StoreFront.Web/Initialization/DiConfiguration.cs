// <copyright file="DiConfiguration.cs" company="Henrik Jensen">
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

using Scrutor;

namespace Hj.EShop.StoreFront.Web.Initialization;

internal static class DiConfiguration
{
    public static IServiceCollection AddScrutorScan(this IServiceCollection services)
    {
        services.Scan(scan => scan
            .FromAssemblyOf<Startup>()
                .AddClasses(c => c.Where(IncludeClass), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Skip)
                .As(t => t.GetInterfaces().Where(IncludeInterface))
                .WithScopedLifetime());

        return services;
    }

    private static bool IncludeClass(Type t)
    {
        bool include = InKnownNamespace(t) && !t.IsGenericType && !t.IsNested && !t.IsAbstract;
        return include;
    }

    private static bool IncludeInterface(Type t)
    {
        bool include = InKnownNamespace(t) && !t.IsGenericType;
        return include;
    }

    private static bool InKnownNamespace(Type t)
    {
        bool isKnownNs = t.Namespace != null && t.Namespace.StartsWith("Hj.EShop", StringComparison.Ordinal);
        return isKnownNs;
    }
}
