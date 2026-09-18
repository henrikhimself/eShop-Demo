// <copyright file="LandingPage.cs" company="Henrik Jensen">
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
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;
using EPiServer.SpecializedProperties;
using EPiServer.Web;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.BaseContent;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms;

[ContentType(
    DisplayName = "Landing Page",
    GUID = "c996e027-1ac1-4221-9f54-c7b8e1236f16")]
public class LandingPage : SitePageBase
{
    [CultureSpecific]
    [Display(
        Name = "Image",
        Order = 100)]
    [AllowedTypes(typeof(ImageMedia))]
    [UIHint(UIHint.Image)]
    public virtual ContentReference? Image { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Heading",
        Order = 110)]
    public virtual string? Heading { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Main Body",
        Order = 120)]
    public virtual XhtmlString? MainBody { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Call To Action Link",
        Order = 130)]
    public virtual LinkItem? CtaLink { get; set; }
}
