// <copyright file="TestCredentials.cs" company="Henrik Jensen">
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

namespace Hj.EShop.AppHost.E2ETests;

internal static class TestCredentials
{
    internal static class SellerPortalSeller
    {
        public const string LoginPath = "bff/login";
        public const string Username = "seller";
        public const string Password = "Sell-1234";
    }

    internal static class StorefrontEditor
    {
        public const string LoginPath = "ui/cms";
        public const string Username = "editor";
        public const string Password = "Edit-1234";
    }

    internal static class StorefrontAdmin
    {
        public const string LoginPath = "ui/cms";
        public const string Username = "admin";
        public const string Password = "Admi-1234";
    }
}
