// <copyright file="BlankSection.cs" company="Henrik Jensen">
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

using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;
using EPiServer.VisualBuilder;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.VisualBuilder;

[ContentType(
    DisplayName = "Blank Section",
    GUID = "f9d73df0-28ba-443c-9bef-74d59d8a3235")]
public class BlankSection : SectionData
{
}
