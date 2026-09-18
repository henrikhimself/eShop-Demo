// <copyright file="VideoMedia.cs" company="Henrik Jensen">
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
    DisplayName = "Video",
    GUID = "0f3d70f0-f21e-4bbb-ab36-1de15927b2b9")]
[MediaDescriptor(ExtensionString = "mp4,webm")]
public class VideoMedia : VideoData
{
}
