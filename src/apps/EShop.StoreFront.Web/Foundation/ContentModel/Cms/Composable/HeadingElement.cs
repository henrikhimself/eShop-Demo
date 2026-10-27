// <copyright file="HeadingElement.cs" company="Henrik Jensen">
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

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.Composable;

[ContentType(
    DisplayName = "Heading",
    GUID = "ea708556-1d5f-43a3-bebb-2f9bb261ae7f",
    CompositionBehaviors = [CompositionBehavior.ElementEnabledKey])]
public class HeadingElement : SiteBlockBase
{
    [CultureSpecific]
    [Display(
        Name = "Heading",
        Order = 100)]
    public virtual string? Heading { get; set; }
}
