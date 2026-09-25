// <copyright file="AppHost.cs" company="Henrik Jensen">
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

using Aspire.Hosting.Azure;
using Aspire.Hosting.JavaScript;
using Hj.EShop.Common;
using Hj.RemoteContainers.Aspire;
using Hj.ReverseProxy.Aspire;
using Microsoft.Extensions.Hosting;
using Projects;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

bool isProductionDeployment = builder.ExecutionContext.IsPublishMode || builder.Environment.IsProduction();

#region SQL server
IResourceBuilder<AzureSqlServerResource> sql = builder.AddAzureSqlServer(KnownNames.ResourceSql)
    .RunAsContainer(container => container
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent));
IResourceBuilder<AzureSqlDatabaseResource> sellerDb = sql.AddDatabase(KnownNames.ResourceSellerDb);
IResourceBuilder<AzureSqlDatabaseResource> storefrontCmsDb = sql.AddDatabase(KnownNames.ResourceStorefrontCmsDb);
IResourceBuilder<AzureSqlDatabaseResource> storefrontCommerceDb = sql.AddDatabase(KnownNames.ResourceStorefrontCommerceDb);
#endregion

#region Service bus
IResourceBuilder<AzureServiceBusResource> serviceBus = builder.AddAzureServiceBus(KnownNames.ResourceServiceBus)
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));

serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerSubmissions);
serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerSubmissionsResult);
serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerSubmissionsCancellations);
serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerInventories);
serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerInventoriesResult);
serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerSubmissionsImageDeletions).Resource.MaxDeliveryCount = 3;
#endregion

#region Storage
IResourceBuilder<AzureStorageResource> storage = builder.AddAzureStorage(KnownNames.ResourceStorage)
    .RunAsEmulator(emulator => emulator
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent));

IResourceBuilder<AzureBlobStorageResource> storageBlob = storage.AddBlobs(KnownNames.ResourceStorageBlob);
IResourceBuilder<AzureBlobStorageContainerResource> submissionsImageContainer = storage.AddBlobContainer(KnownNames.ResourceSellerSubmissionsImage);
_ = storage.AddBlobContainer(KnownNames.ResourceStorefrontStorageBlobContainer);
#endregion

#region Distributed cache
IResourceBuilder<AzureManagedRedisResource> cache = builder.AddAzureManagedRedis(KnownNames.ResourceCache)
    .RunAsContainer(container => container
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent));
#endregion

#region Projects
builder.AddProject<EShop_SellerPortal_MigrationRunner>(KnownNames.ResourceSellerDbMigrationRunner)
    .WithReference(sellerDb);
IResourceBuilder<ProjectResource> sellerPortalBff = builder
    .AddProject<EShop_SellerPortal_Bff>(KnownNames.ResourceSellerPortalBff)
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health")
    .WithReference(sellerDb)
    .WithReference(serviceBus)
    .WithReference(submissionsImageContainer)
    .WithReference(cache);
#pragma warning disable ASPIREJAVASCRIPT001 // AddNextJsApp is experimental as of Aspire 13.4.
IResourceBuilder<NextJsAppResource> sellerPortalWeb = builder
    .AddNextJsApp(KnownNames.ResourceSellerPortalWeb, "../apps/EShop.SellerPortal.Web")
    .WithPnpm()
    .WithReference(sellerPortalBff)
    .WithEnvironment("BFF_URL", sellerPortalBff.GetEndpoint("http"))
    .WithEnvironment("NEXT_DIST_DIR", Environment.GetEnvironmentVariable("NEXT_DIST_DIR") ?? ".next");
#pragma warning restore ASPIREJAVASCRIPT001

builder.AddProject<EShop_StoreFront_MigrationRunner>(KnownNames.ResourceStorefrontCmsMigrationRunner)
    .WithReference(storefrontCmsDb)
    .WithArgs("cms");
builder.AddProject<EShop_StoreFront_MigrationRunner>(KnownNames.ResourceStorefrontCommerceMigrationRunner)
    .WithReference(storefrontCommerceDb)
    .WithArgs("commerce");
IResourceBuilder<ProjectResource> storefrontWeb = builder
    .AddProject<EShop_StoreFront_Web>(KnownNames.ResourceStorefrontWeb)
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ConnectionStrings__EPiServerDB", storefrontCmsDb)
    .WithEnvironment("ConnectionStrings__EcfSqlConnection", storefrontCommerceDb)
    .WithReference(storageBlob)
    .WithReference(cache);
#endregion

#region Development environment
if (!isProductionDeployment)
{
    #region Keycloak
    IResourceBuilder<ParameterResource> keycloakAdminUsername = builder.AddParameter(KnownNames.ResourceKeycloakAdminUsername, "admin");
    IResourceBuilder<ParameterResource> keycloakAdminPassword = builder.AddParameter(KnownNames.ResourceKeycloakAdminPassword, secret: true);

    IResourceBuilder<KeycloakResource> keycloak = builder
        .AddKeycloak(KnownNames.ResourceKeycloak, adminUsername: keycloakAdminUsername, adminPassword: keycloakAdminPassword)
        .WithEnvironment("KC_HOSTNAME", KnownValues.KeycloakPublicOrigin)
        .WithEnvironment("KC_PROXY_HEADERS", "xforwarded")
        .WithRealmImport("./Realms")

        // See doc/CHRONICLE.md — persistent lifetime without WithDataVolume() avoids stale Keycloak signing keys while keeping realm-import edits live.
        .WithLifetime(ContainerLifetime.Persistent);

    IResourceBuilder<ParameterResource> sellerPortalOidcClientSecret = builder.AddParameter(KnownNames.ResourceSellerPortalOidcClientSecret, secret: true);
    sellerPortalBff
        .WithEnvironment(KnownNames.IdentityProviderName, KnownNames.IdentityProviderKeycloakName)
        .WithEnvironment(KnownNames.IdentityProviderKeycloakClientSecret, sellerPortalOidcClientSecret)
        .WithReference(keycloak);

    IResourceBuilder<ParameterResource> storefrontOidcClientSecret = builder.AddParameter(KnownNames.ResourceStorefrontOidcClientSecret, secret: true);
    storefrontWeb
        .WithEnvironment(KnownNames.IdentityProviderName, KnownNames.IdentityProviderKeycloakName)
        .WithEnvironment(KnownNames.IdentityProviderKeycloakClientSecret, storefrontOidcClientSecret)
        .WithReference(keycloak);
    #endregion

    builder.AddProject<EShop_DevTools>(KnownNames.ResourceDevTools)
        .WithHttpEndpoint(name: "http")
        .WithReference(serviceBus)
        .WithReference(submissionsImageContainer)
        .WithExternalHttpEndpoints();

    #region Reverse proxy
    string reverseProxyHome = Environment.GetEnvironmentVariable(KnownNames.ReverseProxyHomeEnvVarName)
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".reverseproxy");
    try
    {
        Directory.CreateDirectory(reverseProxyHome);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        throw new InvalidOperationException($"Cannot create or access the reverse-proxy CA directory '{reverseProxyHome}'.", ex);
    }

    IResourceBuilder<ProjectResource> reverseProxy = builder
        .AddProject<EShop_ReverseProxy>(KnownNames.ResourceDevReverseProxy)
        .WithHttpsEndpoint(KnownNames.ReverseProxyHttpsPort);

    // Separate plain-HTTP health endpoint: the AppHost's own health-check HttpClient doesn't trust the proxy's self-signed dev CA (PartialChain TLS errors).
    reverseProxy.WithHttpEndpoint(name: "health")
        .WithHttpHealthCheck(path: "/health", endpointName: "health")
        .WithEnvironment(KnownNames.ReverseProxyHomeEnvVarName, reverseProxyHome)
        .WaitFor(keycloak)
        .WaitFor(sellerPortalWeb)
        .WaitFor(storefrontWeb);

    // forwardPublicOrigin: true on all three - each target's OIDC redirect_uri construction depends on X-Forwarded-Host/-Proto reflecting the public origin, not an internal address (PLAN-2.md §5).
    reverseProxy
        .WithReverseProxyReference(keycloak.GetEndpoint("http"), KnownNames.ReverseProxyIdentityHostName, true)
        .WithReverseProxyReference(sellerPortalWeb.GetEndpoint("http"), KnownNames.ReverseProxySellerPortalHostName, true)
        .WithReverseProxyReference(storefrontWeb.GetEndpoint("http"), KnownNames.ReverseProxyStorefrontHostName, true);
    #endregion

    builder.AddSshTunneling();
}
#endregion

#region Production environment
if (isProductionDeployment)
{
    builder.AddAzureContainerAppEnvironment(KnownNames.ResourceAcaEnvironment);

    sellerPortalWeb.WithExternalHttpEndpoints();
    storefrontWeb.WithExternalHttpEndpoints();

    IResourceBuilder<ParameterResource> entraTenantSubdomain = builder.AddParameter(KnownNames.ResourceEntraTenantSubdomain);

    IResourceBuilder<ParameterResource> sellerEntraClientId = builder.AddParameter(KnownNames.ResourceEntraClientId);
    IResourceBuilder<ParameterResource> sellerEntraClientSecret = builder.AddParameter(KnownNames.ResourceEntraClientSecret, secret: true);
    sellerPortalBff
        .WithEnvironment(KnownNames.IdentityProviderName, KnownNames.IdentityProviderEntraExternalId)
        .WithEnvironment(KnownNames.IdentityProviderEntraTenantSubdomain, entraTenantSubdomain)
        .WithEnvironment(KnownNames.IdentityProviderEntraClientId, sellerEntraClientId)
        .WithEnvironment(KnownNames.IdentityProviderEntraClientSecret, sellerEntraClientSecret);

    IResourceBuilder<ParameterResource> storefrontEntraClientId = builder.AddParameter(KnownNames.ResourceStorefrontEntraClientId);
    IResourceBuilder<ParameterResource> storefrontEntraClientSecret = builder.AddParameter(KnownNames.ResourceStorefrontEntraClientSecret, secret: true);
    storefrontWeb
        .WithEnvironment(KnownNames.IdentityProviderName, KnownNames.IdentityProviderEntraExternalId)
        .WithEnvironment(KnownNames.IdentityProviderEntraTenantSubdomain, entraTenantSubdomain)
        .WithEnvironment(KnownNames.IdentityProviderEntraClientId, storefrontEntraClientId)
        .WithEnvironment(KnownNames.IdentityProviderEntraClientSecret, storefrontEntraClientSecret);
}
#endregion

await builder.Build().RunAsync();
