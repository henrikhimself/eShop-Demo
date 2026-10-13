// <copyright file="MovieProduct.cs" company="Henrik Jensen">
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
    DisplayName = "Movie",
    GUID = "e9e0b060-1bc8-46a4-86ea-7ad68de19c4c",
    MetaClassName = nameof(MovieProduct))]
public class MovieProduct : SiteProductBase
{
    [CultureSpecific]
    [Display(
        Name = "Poster Image",
        Order = 100)]
    [AllowedTypes(typeof(ImageMedia))]
    [UIHint(UIHint.Image)]
    public virtual ContentReference? PosterImage { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Trailer Video",
        Order = 110)]
    [AllowedTypes(typeof(VideoMedia))]
    [UIHint(UIHint.Video)]
    public virtual ContentReference? TrailerVideo { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Description",
        Order = 120)]
    [UIHint(UIHint.Textarea)]
    public virtual string? Description { get; set; }
}
