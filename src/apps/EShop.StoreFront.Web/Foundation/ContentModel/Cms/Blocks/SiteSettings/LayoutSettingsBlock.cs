// <copyright file="LayoutSettingsBlock.cs" company="Henrik Jensen">
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
using EPiServer.DataAnnotations;
using EPiServer.Forms.Core;
using EPiServer.Web;
using Hj.EShop.StoreFront.Web.Foundation.SiteSettings;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.Blocks.SiteSettings;

[ContentType(
    DisplayName = "Layout Settings",
    GUID = "eff62260-2e12-4b3d-b79e-ea83f9f232fc")]
public class LayoutSettingsBlock : BlockBase, ISiteSettingsBlock
{
    [Display(
        Name = "Logo",
        Order = 100)]
    [AllowedTypes(typeof(ImageMedia))]
    [UIHint(UIHint.Image)]
    public virtual ContentReference? Logo { get; set; }
}
