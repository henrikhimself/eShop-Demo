// <copyright file="MigrationNames.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Migrations.Common;

// Component/lock names each migration runner writes and each independent reader
// (SchemaMarkerHealthCheck in EShop.SellerPortal.Bff, StorefrontMigrationPreflight in
// EShop.StoreFront.Web) reads - kept in one place so they can't drift. Moved out of
// EShop.Common's KnownNames: that class is referenced by nearly every project in the
// repo, most of which have nothing to do with migrations.
public static class MigrationNames
{
    public const string SellerPortalComponent = "seller-portal-db";
    public const string SellerPortalLockName = "seller-portal-db-migration";
    public const string StorefrontCmsComponent = "storefront-cms-db";
    public const string StorefrontCmsLockName = "storefront-cms-db-migration";
    public const string StorefrontCommerceComponent = "storefront-commerce-db";
    public const string StorefrontCommerceLockName = "storefront-commerce-db-migration";
}
