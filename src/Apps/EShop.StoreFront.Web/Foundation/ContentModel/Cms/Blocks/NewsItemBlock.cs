// <copyright file="NewsItemBlock.cs" company="Henrik Jensen">
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

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.Blocks;

[ContentType(
    DisplayName = "News Item",
    GUID = "1b7bebab-bc9c-492f-9dbb-2ef95b48faf6")]
[AvailableContentTypes(IncludeOn = [typeof(NewsListPage)])]
public class NewsItemBlock : BlockData
{
    [CultureSpecific]
    [Display(
        Name = "Title",
        Order = 10)]
    public virtual string? Title { get; set; }

    [CultureSpecific]
    [Display(
        Name = "Main Body",
        Order = 20)]
    [UIHint(UIHint.Textarea)]
    public virtual string? MainBody { get; set; }
}
