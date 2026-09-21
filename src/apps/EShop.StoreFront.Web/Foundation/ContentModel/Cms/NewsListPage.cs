// <copyright file="NewsListPage.cs" company="Henrik Jensen">
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
using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.BaseContent;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.Blocks;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms;

[ContentType(
    DisplayName = "News List",
    GUID = "d28e12d0-32ee-4a1a-a5fe-98b271bf4069")]
public class NewsListPage : SitePageBase
{
    [Display(
        Name = "News Items",
        Order = 100)]
    public virtual IList<NewsItemBlock>? NewsItems { get; set; }
}
