// <copyright file="ServiceMenuSettingsBlock.cs" company="Henrik Jensen">
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
using EPiServer.DataAnnotations;
using EPiServer.Forms.Core;
using EPiServer.SpecializedProperties;
using Hj.EShop.StoreFront.Web.Foundation.SiteSettings;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.Blocks.SiteSettings;

[ContentType(
    DisplayName = "Service Menu Settings",
    GUID = "328edd40-4313-4f56-bd6c-da4b5c6b9f93")]
public class ServiceMenuSettingsBlock : BlockBase, ISiteSettingsBlock
{
    [CultureSpecific]
    [Display(
        Name = "Navigation Links",
        Description = "A list of links to display in the service menu.",
        Order = 100)]
    public virtual LinkItemCollection? NavigationLinks { get; set; }
}
