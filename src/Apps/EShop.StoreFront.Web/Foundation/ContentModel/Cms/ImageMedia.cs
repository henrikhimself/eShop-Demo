// <copyright file="ImageMedia.cs" company="Henrik Jensen">
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
using EPiServer.Framework.DataAnnotations;

namespace Hj.EShop.StoreFront.Web.Foundation.ContentModel.Cms;

[ContentType(
    DisplayName = "Image",
    GUID = "c9da2b70-6050-4f97-807e-b0094c871824")]
[MediaDescriptor(ExtensionString = "gif,jpg,jpeg,jfif,pjpeg,pjp,png,svg,webp")]
public class ImageMedia : ImageData
{
}
