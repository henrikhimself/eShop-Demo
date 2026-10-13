// <copyright file="FeaturesControllerConvention.cs" company="Henrik Jensen">
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

using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Hj.EShop.StoreFront.Web.Foundation.Conventions;

internal sealed class FeaturesControllerConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        const string NsSegment = "Features";

        string[]? ns = controller.ControllerType.Namespace?.Split('.');
        if (ns is null || !ns.Any(t => t == NsSegment))
        {
            return;
        }

        string? name = ns
          .SkipWhile(t => !t.Equals(NsSegment, StringComparison.Ordinal))
          .Skip(1)
          .Take(1)
          .FirstOrDefault();
        controller.SetFeatureName(name);
    }
}
