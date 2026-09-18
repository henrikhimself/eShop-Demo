// <copyright file="MoviePackage.cs" company="Henrik Jensen">
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
    DisplayName = "Package",
    GUID = "16f37971-ea5f-4ca7-b801-a6be58097a92",
    MetaClassName = nameof(MoviePackage))]
public class MoviePackage : SitePackageBase
{
    [CultureSpecific]
    [Display(
        Name = "Package Image",
        Order = 100)]
    [AllowedTypes(typeof(ImageMedia))]
    [UIHint(UIHint.Image)]
    public virtual ContentReference? PackageImage { get; set; }
}
