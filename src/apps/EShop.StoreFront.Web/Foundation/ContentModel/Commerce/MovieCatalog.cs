// <copyright file="MovieCatalog.cs" company="Henrik Jensen">
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

using EPiServer.Commerce.Catalog.DataAnnotations;
using Hj.EShop.StoreFront.Web.Foundation.ContentModel.Commerce.BaseContent;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Commerce;

[CatalogContentType(
    DisplayName = "Movie Catalog",
    GUID = "4d3b52d9-e0b7-4db1-8fbf-09f466259b39",
    MetaClassName = nameof(MovieCatalog))]
public class MovieCatalog : SiteCatalogBase
{
}
