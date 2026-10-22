// <copyright file="SiteSettingsPage.cs" company="Henrik Jensen">
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
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Internal;
using Hj.EShop.StoreFront.Web.Foundation.DisableIndexing.Contracts;
using Hj.EShop.StoreFront.Web.Foundation.SiteSettings.Contracts;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms;

[ContentType(
    DisplayName = "Site Settings",
    GUID = "b00db64f-7216-4881-b660-98d94d32ec4e")]
public class SiteSettingsPage : PageData, IDisableIndexing
{
    [IndexingType(IndexingType.Disabled)]
    [ScaffoldColumn(false)]
    public virtual bool? DisableIndexing { get; set; }

    [IndexingType(IndexingType.Disabled)]
    [CultureSpecific]
    [Display(
        Name = "Site Settings",
        Order = 100)]
    [AllowedTypes(typeof(ISiteSettingsBlock))]
    public virtual ContentArea? SiteSettings { get; set; }

    public override void SetDefaultValues(ContentType contentType)
    {
        base.SetDefaultValues(contentType);
        VisibleInMenu = false;
        DisableIndexing = true;
        ContentDefaults.ApplyDefaultsAttribute(this, contentType);
    }
}
