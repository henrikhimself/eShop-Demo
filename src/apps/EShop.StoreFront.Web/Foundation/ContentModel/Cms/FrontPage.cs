// <copyright file="FrontPage.cs" company="Henrik Jensen">
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
using EPiServer.Web;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.BaseContent;
using Hj.EShop.StoreFront.Web.Foundation.SiteSettings;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms;

[ContentType(
    DisplayName = "Frontpage",
    GUID = "3c952f63-1e40-4288-8545-548ce6d103d4")]
public class FrontPage : SitePageBase, ISiteSettingsPage
{
    [CultureSpecific]
    [Display(
        Name = "Site Settings",
        Order = 10,
        GroupName = SystemTabNames.Settings)]
    [AllowedTypes(typeof(ISiteSettingsBlock))]
    public virtual ContentArea? SiteSettings { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Hero Header",
        Order = 100)]
    public virtual string? HeroHeader { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Hero Byline",
        Order = 110)]
    public virtual string? HeroByline { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Hero Image",
        Order = 120)]
    [AllowedTypes(typeof(ImageMedia))]
    [UIHint(UIHint.Image)]
    public virtual ContentReference? HeroImage { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Hero Content",
        Order = 130)]
    public virtual ContentArea? HeroContent { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Watch On TV Content",
        Order = 200)]
    public virtual XhtmlString? WatchOnTvContent { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Watch On TV Image",
        Order = 210)]
    [AllowedTypes(typeof(ImageMedia))]
    [UIHint(UIHint.Image)]
    public virtual ContentReference? WatchOnTvImage { get; set; }
}
