// <copyright file="BodyTextElement.cs" company="Henrik Jensen">
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
using EPiServer.Web;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.BaseContent;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms.Composable;

[ContentType(
    DisplayName = "Body Text",
    GUID = "7b48214d-66d9-43a9-9e47-a089aadd5d67",
    CompositionBehaviors = [CompositionBehavior.ElementEnabledKey])]
public class BodyTextElement : SiteBlockBase
{
    [CultureSpecific]
    [Display(
        Name = "Body Text",
        Order = 1000)]
    [UIHint(UIHint.Textarea)]
    public virtual string? BodyText { get; set; }
}
