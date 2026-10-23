// <copyright file="IDisableIndexing.cs" company="Henrik Jensen">
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

using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;

namespace Hj.EShop.StoreFront.Web.Foundation.DisableIndexing.Contracts;

[ContentType(
    GUID = "155ae549-9c1f-41bd-9156-9cc829086bae",
    DisplayName = "Disable Indexing",
    Description = "Adds option to disable content from being indexed.")]
internal interface IDisableIndexing : IContentData
{
    const string PropertyName = "Disable indexing";
    const string PropertyDescription = "Disables indexing when selected. Rebuild your index to remove content that has already been indexed.";
    const string PropertyGroupName = SystemTabNames.Settings;
    const int PropertyOrder = -100;

    bool? DisableIndexing { get; set; }
}
