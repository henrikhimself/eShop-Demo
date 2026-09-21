// <copyright file="MovieBundle.cs" company="Henrik Jensen">
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

using System.ComponentModel.DataAnnotations;
using EPiServer.Commerce.Catalog.DataAnnotations;
using EPiServer.Core;
using EPiServer.DataAnnotations;
using EPiServer.Web;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Commerce.BaseContent;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Commerce;

[CatalogContentType(
    DisplayName = "Bundle",
    GUID = "6225341c-ed52-41e7-acc3-436dc0661a1f",
    MetaClassName = nameof(MovieBundle))]
public class MovieBundle : SiteBundleBase
{
    [CultureSpecific]
    [Display(
        Name = "Bundle Image",
        Order = 100)]
    [AllowedTypes(typeof(ImageMedia))]
    [UIHint(UIHint.Image)]
    public virtual ContentReference? BundleImage { get; set; }
}
