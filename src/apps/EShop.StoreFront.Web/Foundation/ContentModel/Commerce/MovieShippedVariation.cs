// <copyright file="MovieShippedVariation.cs" company="Henrik Jensen">
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

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Commerce;

[CatalogContentType(
    DisplayName = "Variation",
    GUID = "a41c5610-f520-4700-be07-af87c99288a9",
    MetaClassName = nameof(MovieShippedVariation))]
public class MovieShippedVariation : MovieVariationBase
{
}
