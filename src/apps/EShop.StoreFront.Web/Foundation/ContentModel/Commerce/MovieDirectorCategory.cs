// <copyright file="MovieDirectorCategory.cs" company="Henrik Jensen">
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
using EPiServer.Commerce.Catalog.DataAnnotations;
using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Commerce.BaseContent;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Commerce;

[CatalogContentType(
    DisplayName = "Director Category",
    GUID = "e81b514e-7ba5-430f-9075-94aeedd1c3f5",
    MetaClassName = nameof(MovieDirectorCategory))]
public class MovieDirectorCategory : SiteNodeBase
{
    [IndexingType(IndexingType.Searchable)]
    [Display(
        Name = "Director Name",
        Order = 100)]
    public virtual string? DirectoryName { get; set; }
}
