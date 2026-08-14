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
using Hj.EShop.AppHost;
using Hj.EShop.Common;
using Microsoft.Extensions.DependencyInjection;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// ADR 0018: environment placement matters only when publishing.
if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureContainerAppEnvironment(KnownNames.ResourceAcaEnvironment);
}

// ADR 0019: RunAsContainer does not affect the publish manifest.
IResourceBuilder<AzureSqlServerResource> sql = builder.AddAzureSqlServer(KnownNames.ResourceSql)
    .RunAsContainer(container => container
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent));

IResourceBuilder<AzureSqlDatabaseResource> sellerDb = sql.AddDatabase(KnownNames.ResourceSellerDb);

builder.AddProject<Projects.EShop_SellerPortal_MigrationRunner>(KnownNames.ResourceSellerDbMigrationRunner)
    .WithReference(sellerDb);

IResourceBuilder<AzureSqlDatabaseResource> storefrontCmsDb = sql.AddDatabase(KnownNames.ResourceStorefrontCmsDb);
IResourceBuilder<AzureSqlDatabaseResource> storefrontCommerceDb = sql.AddDatabase(KnownNames.ResourceStorefrontCommerceDb);

builder.AddProject<Projects.EShop_StoreFront_MigrationRunner>(KnownNames.ResourceStorefrontCmsMigrationRunner)
    .WithReference(storefrontCmsDb)
    .WithArgs("cms");
builder.AddProject<Projects.EShop_StoreFront_MigrationRunner>(KnownNames.ResourceStorefrontCommerceMigrationRunner)
    .WithReference(storefrontCommerceDb)
    .WithArgs("commerce");

IResourceBuilder<AzureServiceBusResource> serviceBus = builder.AddAzureServiceBus(KnownNames.ResourceServiceBus)
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));

serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerSubmissions);
serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerSubmissionsResult);
serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerSubmissionsCancellations);

IResourceBuilder<AzureServiceBusQueueResource> submissionsImageDeletions =
    serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerSubmissionsImageDeletions);
submissionsImageDeletions.Resource.MaxDeliveryCount = 3;

serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerInventories);
serviceBus.AddServiceBusQueue(KnownNames.ResourceSellerInventoriesResult);

IResourceBuilder<AzureStorageResource> storage = builder.AddAzureStorage(KnownNames.ResourceStorage)
    .RunAsEmulator(emulator => emulator
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent));

IResourceBuilder<AzureBlobStorageContainerResource> submissionsImageContainer =
    storage.AddBlobContainer(KnownNames.ResourceSellerSubmissionsImage);

IResourceBuilder<AzureManagedRedisResource> cache = builder.AddAzureManagedRedis(KnownNames.ResourceCache)
    .RunAsContainer(container => container
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent));

// Bff is internal-only behind the Seller Portal reverse proxy; readiness is via
// /health (ADR 0018).
IResourceBuilder<ProjectResource> sellerPortalBff = builder
    .AddProject<Projects.EShop_SellerPortal_Bff>(KnownNames.ResourceSellerPortalBff)
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health")
    .WithReference(sellerDb)
    .WithReference(serviceBus)
    .WithReference(submissionsImageContainer)
    .WithReference(cache);

#pragma warning disable ASPIREJAVASCRIPT001 // AddNextJsApp is experimental as of Aspire 13.4.

IResourceBuilder<NextJsAppResource> sellerPortalWeb = builder
    .AddNextJsApp(KnownNames.ResourceSellerPortalWeb, "../EShop.SellerPortal.Web")
    .WithPnpm()
    .WithReference(sellerPortalBff)
    .WithEnvironment("BFF_URL", sellerPortalBff.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

#pragma warning restore ASPIREJAVASCRIPT001

if (!builder.ExecutionContext.IsPublishMode)
{
    builder.AddProject<Projects.EShop_DevTools>(KnownNames.ResourceDevTools)
        .WithHttpEndpoint(name: "http")
        .WithReference(serviceBus)
        .WithReference(submissionsImageContainer)
        .WithExternalHttpEndpoints();
}

// Optimizely expects these exact connection-string keys, not the Aspire resource names.
IResourceBuilder<ProjectResource> storefrontWeb = builder
    .AddProject<Projects.EShop_StoreFront_Web>(KnownNames.ResourceStorefrontWeb)
    .WithHttpEndpoint(name: "http")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ConnectionStrings__EPiServerDB", storefrontCmsDb)
    .WithEnvironment("ConnectionStrings__EcfSqlConnection", storefrontCommerceDb)
    .WithExternalHttpEndpoints();

// Keep build-output separation in sync with next.config.ts's NEXT_DIST_DIR handling.
string? nextDistDir = Environment.GetEnvironmentVariable("NEXT_DIST_DIR");
if (nextDistDir is not null)
{
    sellerPortalWeb.WithEnvironment("NEXT_DIST_DIR", nextDistDir);
}

// Split guard: Keycloak needs local resources (ADR 0010), Entra needs parameters only
// (ADR 0020). Kept together because callbacks use sellerPortalWeb.
if (!builder.ExecutionContext.IsPublishMode)
{
    // Keycloak has no persistent volume here; provision the client dynamically below.
    IResourceBuilder<ParameterResource> keycloakAdminUsername = builder.AddParameter(KnownNames.ResourceKeycloakAdminUsername, "admin");
    IResourceBuilder<ParameterResource> keycloakAdminPassword = builder.AddParameter(KnownNames.ResourceKeycloakAdminPassword, secret: true);
    IResourceBuilder<KeycloakResource> keycloak = builder.AddKeycloak(KnownNames.ResourceKeycloak, adminUsername: keycloakAdminUsername, adminPassword: keycloakAdminPassword)
        .WithRealmImport("./Realms");

    IResourceBuilder<ParameterResource> sellerPortalOidcClientSecret = builder.AddParameter(
        KnownNames.ResourceSellerPortalOidcClientSecret, secret: true);
    IResourceBuilder<ParameterResource> storefrontOidcClientSecret = builder.AddParameter(
        KnownNames.ResourceStorefrontOidcClientSecret, secret: true);

    sellerPortalBff
        .WithEnvironment("Identity__Provider", KnownNames.IdentityProviderKeycloak)
        .WithEnvironment("Keycloak__ClientSecret", sellerPortalOidcClientSecret)
        .WithReference(keycloak);

    storefrontWeb
        .WithEnvironment("Identity__Provider", KnownNames.IdentityProviderKeycloak)
        .WithEnvironment("Keycloak__ClientSecret", storefrontOidcClientSecret)
        .WithReference(keycloak);

    // Provision Seller Portal Keycloak client once the real endpoint is allocated.
    sellerPortalWeb.OnResourceReady(async (resource, @event, cancellationToken) =>
    {
        ResourceNotificationService resourceNotificationService =
            @event.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceHealthyAsync(KnownNames.ResourceKeycloak, cancellationToken);

        await KeycloakSellerPortalClientProvisioner.ProvisionAsync(
            keycloak,
            keycloakAdminUsername,
            keycloakAdminPassword,
            sellerPortalOidcClientSecret,
            new Uri(resource.GetEndpoint("http").Url),
            KnownNames.SellerPortalOidcClientId,
            KnownNames.KeycloakRealmEShop,
            cancellationToken);
    });

    // Same pattern for Storefront; independent client scope/roles.
    storefrontWeb.OnResourceReady(async (resource, @event, cancellationToken) =>
    {
        ResourceNotificationService resourceNotificationService =
            @event.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceHealthyAsync(KnownNames.ResourceKeycloak, cancellationToken);

        await KeycloakStorefrontClientProvisioner.ProvisionAsync(
            keycloak,
            keycloakAdminUsername,
            keycloakAdminPassword,
            storefrontOidcClientSecret,
            new Uri(resource.GetEndpoint("http").Url),
            KnownNames.StorefrontOidcClientId,
            KnownNames.KeycloakRealmEShop,
            cancellationToken);
    });
}
else
{
    // See doc/adr/0020-entra-external-id-production-identity-provider.md — manual Entra
    // setup; these three parameters have no default, so `aspire deploy` prompts for them.
    IResourceBuilder<ParameterResource> entraTenantSubdomain = builder.AddParameter(KnownNames.ResourceEntraTenantSubdomain);
    IResourceBuilder<ParameterResource> entraClientId = builder.AddParameter(KnownNames.ResourceEntraClientId);
    IResourceBuilder<ParameterResource> entraClientSecret = builder.AddParameter(KnownNames.ResourceEntraClientSecret, secret: true);

    sellerPortalBff
        .WithEnvironment("Identity__Provider", KnownNames.IdentityProviderEntraExternalId)
        .WithEnvironment("Identity__Entra__TenantSubdomain", entraTenantSubdomain)
        .WithEnvironment("Identity__Entra__ClientId", entraClientId)
        .WithEnvironment("Identity__Entra__ClientSecret", entraClientSecret);

    // Storefront's staff actors are a separate Entra App Registration from the Seller
    // Portal's Seller-facing one (different roles, different audience) - same tenant,
    // its own client id/secret parameters.
    IResourceBuilder<ParameterResource> storefrontEntraClientId = builder.AddParameter(KnownNames.ResourceStorefrontEntraClientId);
    IResourceBuilder<ParameterResource> storefrontEntraClientSecret =
        builder.AddParameter(KnownNames.ResourceStorefrontEntraClientSecret, secret: true);

    storefrontWeb
        .WithEnvironment("Identity__Provider", KnownNames.IdentityProviderEntraExternalId)
        .WithEnvironment("Identity__Entra__TenantSubdomain", entraTenantSubdomain)
        .WithEnvironment("Identity__Entra__ClientId", storefrontEntraClientId)
        .WithEnvironment("Identity__Entra__ClientSecret", storefrontEntraClientSecret);
}

await builder.Build().RunAsync();
