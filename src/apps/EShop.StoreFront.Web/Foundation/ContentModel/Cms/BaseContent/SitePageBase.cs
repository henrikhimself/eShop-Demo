// <copyright file="SitePageBase.cs" company="Henrik Jensen">
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

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.BaseContent;

public abstract class SitePageBase : PageData, IDisableIndexing
{
    [IndexingType(IndexingType.Disabled)]
    [Display(
        Name = IDisableIndexing.PropertyName,
        Description = IDisableIndexing.PropertyDescription,
        Order = IDisableIndexing.PropertyOrder,
        GroupName = IDisableIndexing.PropertyGroupName)]
    public virtual bool? DisableIndexing { get; set; }

    public override void SetDefaultValues(ContentType contentType)
    {
        base.SetDefaultValues(contentType);
        ContentDefaults.ApplyDefaultsAttribute(this, contentType);
    }
}
