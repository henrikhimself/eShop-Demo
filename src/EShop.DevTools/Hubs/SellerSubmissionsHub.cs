// <copyright file="SellerSubmissionsHub.cs" company="Henrik Jensen">
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

using Microsoft.AspNetCore.SignalR;

namespace Hj.EShop.DevTools.Hubs;

// Push-only broadcast hub. See doc/MEMORY.md - EShop.DevTools has no authentication.
internal sealed class SellerSubmissionsHub : Hub
{
    // Shared by the server broadcast calls, the Razor page's client script, and the
    // tests, so they can't drift from each other.
    public const string SubmissionsChangedEvent = "submissionsChanged";
}
