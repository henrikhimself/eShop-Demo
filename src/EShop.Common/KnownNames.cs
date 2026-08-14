// <copyright file="KnownNames.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Common;

public static class KnownNames
{
    public const string ResourceAcaEnvironment = "aca-env";
    public const string ResourceSql = "sql";
    public const string ResourceSellerDb = "seller-db";

    public const string ResourceStorefrontCmsDb = "storefront-cms-db";
    public const string ResourceStorefrontCommerceDb = "storefront-commerce-db";
    public const string ResourceStorefrontCmsMigrationRunner = "storefront-cms-db-migration-runner";
    public const string ResourceStorefrontCommerceMigrationRunner = "storefront-commerce-db-migration-runner";
    public const string ResourceStorefrontWeb = "storefront-web";
    public const string ResourceServiceBus = "service-bus";
    public const string ResourceSellerSubmissions = "seller-submissions";
    public const string ResourceSellerSubmissionsResult = "seller-submissions-result";
    public const string ResourceSellerSubmissionsImage = "seller-submissions-image";
    public const string ResourceSellerSubmissionsImageDeletions = "seller-submissions-image-deletions";
    public const string ResourceSellerSubmissionsCancellations = "seller-submissions-cancellations";
    public const string ResourceStorage = "storage";
    public const string ResourceCache = "cache";
    public const string ResourceKeycloak = "keycloak";
    public const string ResourceKeycloakAdminUsername = "keycloak-admin-username";
    public const string ResourceKeycloakAdminPassword = "keycloak-admin-password";
    public const string ResourceSellerPortalBff = "seller-portal-bff";
    public const string ResourceSellerDbMigrationRunner = "seller-db-migration-runner";
    public const string ResourceSellerPortalWeb = "seller-portal-web";
    public const string ResourceDevTools = "dev-tools";
    public const string ResourceSellerInventories = "seller-inventories";
    public const string ResourceSellerInventoriesResult = "seller-inventories-result";
    public const string ResourceSellerPortalOidcClientSecret = "seller-portal-oidc-client-secret";
    public const string ResourceStorefrontOidcClientSecret = "storefront-oidc-client-secret";
    public const string ResourceEntraTenantSubdomain = "entra-tenant-subdomain";
    public const string ResourceEntraClientId = "entra-client-id";
    public const string ResourceEntraClientSecret = "entra-client-secret";
    public const string ResourceStorefrontEntraClientId = "storefront-entra-client-id";
    public const string ResourceStorefrontEntraClientSecret = "storefront-entra-client-secret";

    public const string KeycloakRealmEShop = "eshop";
    public const string SellerPortalOidcClientId = "seller-portal";
    public const string StorefrontOidcClientId = "storefront";

    // Identity__Provider value AppHost.cs sets on the Bff (ADR 0002/ADR 0010/ADR 0020) -
    // shared so AppHost.cs and Program.cs can't drift on the literal string.
    public const string IdentityProviderKeycloak = "Keycloak";
    public const string IdentityProviderEntraExternalId = "EntraExternalId";
}
