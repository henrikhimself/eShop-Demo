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
    public const string FakeEnvironmentName = "Fake";

    public const string ResourceAcaEnvironment = "aca-env";

    public const string ResourceSql = "sql";

    public const string ResourceServiceBus = "service-bus";

    public const string ResourceStorage = "storage";
    public const string ResourceStorageBlob = "storage-blob";

    public const string ResourceCache = "cache";

    public const string ResourceDevTools = "dev-tools";
    public const string ResourceDevReverseProxy = "reverse-proxy";
    public const string ReverseProxySellerPortalHostName = "seller.eshop.local";
    public const string ReverseProxyStorefrontHostName = "storefront.eshop.local";
    public const string ReverseProxyIdentityHostName = "identity.eshop.local";
    public const int ReverseProxyHttpsPort = 8443;
    public const string ReverseProxyHomeEnvVarName = "REVERSEPROXY_HOME";

    public const string ResourceSellerDb = "seller-db";
    public const string SellerPortalOidcClientId = "seller-portal";
    public const string ResourceSellerSubmissions = "seller-submissions";
    public const string ResourceSellerSubmissionsResult = "seller-submissions-result";
    public const string ResourceSellerSubmissionsImage = "seller-submissions-image";
    public const string ResourceSellerSubmissionsImageDeletions = "seller-submissions-image-deletions";
    public const string ResourceSellerSubmissionsCancellations = "seller-submissions-cancellations";
    public const string ResourceSellerPortalBff = "seller-portal-bff";
    public const string ResourceSellerDbMigrationRunner = "seller-db-migration-runner";
    public const string ResourceSellerPortalWeb = "seller-portal-web";
    public const string ResourceSellerInventories = "seller-inventories";
    public const string ResourceSellerInventoriesResult = "seller-inventories-result";
    public const string ResourceSellerPortalOidcClientSecret = "seller-portal-oidc-client-secret";

    public const string ResourceStorefrontCmsDb = "storefront-cms-db";
    public const string ResourceStorefrontCommerceDb = "storefront-commerce-db";
    public const string ResourceStorefrontCmsMigrationRunner = "storefront-cms-db-migration-runner";
    public const string ResourceStorefrontCommerceMigrationRunner = "storefront-commerce-db-migration-runner";
    public const string ResourceStorefrontWeb = "storefront-web";
    public const string ResourceStorefrontOidcClientSecret = "storefront-oidc-client-secret";
    public const string ResourceStorefrontEntraClientId = "storefront-entra-client-id";
    public const string ResourceStorefrontEntraClientSecret = "storefront-entra-client-secret";
    public const string StorefrontOidcClientId = "storefront";
    public const string ResourceStorefrontStorageBlobContainer = "storefront-blob-cms";

    public const string IdentityProviderName = "Identity__Provider";

    public const string ResourceKeycloak = "keycloak";
    public const string ResourceKeycloakAdminUsername = "keycloak-admin-username";
    public const string ResourceKeycloakAdminPassword = "keycloak-admin-password";
    public const string KeycloakRealmEShop = "eshop";
    public const string IdentityProviderKeycloakName = "Keycloak";
    public const string IdentityProviderKeycloakClientSecret = "Keycloak__ClientSecret";

    public const string ResourceEntraTenantSubdomain = "entra-tenant-subdomain";
    public const string ResourceEntraClientId = "entra-client-id";
    public const string ResourceEntraClientSecret = "entra-client-secret";
    public const string IdentityProviderEntraExternalId = "EntraExternalId";
    public const string IdentityProviderEntraTenantSubdomain = "Identity__Entra__TenantSubdomain";
    public const string IdentityProviderEntraClientId = "Identity__Entra__ClientId";
    public const string IdentityProviderEntraClientSecret = "Identity__Entra__ClientSecret";
}
