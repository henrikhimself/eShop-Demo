// <copyright file="FeaturesViewLocationExpander.cs" company="Henrik Jensen">
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

using Microsoft.AspNetCore.Mvc.Razor;

namespace Hj.EShop.StoreFront.Web.Foundation.Conventions;

internal sealed class FeaturesViewLocationExpander : IViewLocationExpander
{
    private const string FeatureName = "FeatureName";

    private readonly List<string> _viewLocationFormats = [
        "/Features/{3}/Views/{1}/{0}.cshtml",
        "/Features/{3}/Views/Shared/{0}.cshtml",
        "/Features/{3}/{0}.cshtml"];

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        if (context.Values.TryGetValue(FeatureName, out string? featureName))
        {
            foreach (string item in ExpandFeaturesViewLocations(_viewLocationFormats.Concat(viewLocations), featureName))
            {
                yield return item;
            }
        }
        else
        {
            foreach (string location in viewLocations)
            {
                yield return location;
            }
        }
    }

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        string? featureName = context.ActionContext.GetFeatureName();
        if (!string.IsNullOrWhiteSpace(featureName))
        {
            context.Values[FeatureName] = context.ActionContext.GetFeatureName();
        }
    }

    private static IEnumerable<string> ExpandFeaturesViewLocations(IEnumerable<string> viewLocations, string? featureName)
    {
        foreach (string location in viewLocations)
        {
            yield return location.Replace("{3}", featureName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
