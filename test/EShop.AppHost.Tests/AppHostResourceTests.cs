using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Hj.EShop.Common;
using Xunit;

namespace Hj.EShop.AppHost.Tests;

public sealed class AppHostResourceTests
{
    [Fact]
    public async Task AppHost_RegistersExpectedResources()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(cancellationToken: TestContext.Current.CancellationToken);

        string[] resourceNames = [.. builder.Resources.Select(resource => resource.Name)];

        Assert.Contains(KnownNames.ResourceSql, resourceNames);
        Assert.Contains(KnownNames.ResourceSellerDb, resourceNames);
        Assert.Contains(KnownNames.ResourceServiceBus, resourceNames);
        Assert.Contains(KnownNames.ResourceSellerSubmissions, resourceNames);
        Assert.Contains(KnownNames.ResourceSellerSubmissionsResult, resourceNames);
        Assert.Contains(KnownNames.ResourceSellerSubmissionsImageDeletions, resourceNames);
        Assert.Contains(KnownNames.ResourceStorage, resourceNames);
        Assert.Contains(KnownNames.ResourceSellerSubmissionsImage, resourceNames);
        Assert.Contains(KnownNames.ResourceCache, resourceNames);
        Assert.Contains(KnownNames.ResourceKeycloak, resourceNames);
        Assert.Contains(KnownNames.ResourceSellerInventories, resourceNames);
        Assert.Contains(KnownNames.ResourceSellerInventoriesResult, resourceNames);
        Assert.Contains(KnownNames.ResourceSellerPortalBff, resourceNames);
        Assert.Contains(KnownNames.ResourceSellerPortalWeb, resourceNames);
        Assert.Contains(KnownNames.ResourceDevTools, resourceNames);
        Assert.Contains(KnownNames.ResourceStorefrontCmsDb, resourceNames);
        Assert.Contains(KnownNames.ResourceStorefrontCommerceDb, resourceNames);
        Assert.Contains(KnownNames.ResourceStorefrontCmsMigrationRunner, resourceNames);
        Assert.Contains(KnownNames.ResourceStorefrontCommerceMigrationRunner, resourceNames);
        Assert.Contains(KnownNames.ResourceStorefrontWeb, resourceNames);
    }

    // ADR 0023, extended to Optimizely (see doc/CHRONICLE.md): Optimizely's own
    // DatabaseSchemaHost crashes the whole app at startup on a missing schema, and
    // .WaitFor has no effect once deployed to Azure Container Apps - so unlike the
    // Seller Portal Bff, the Storefront web resource must not rely on AppHost-level
    // ordering at all (no .WaitFor on either migration runner).
    [Fact]
    public async Task AppHost_StorefrontWeb_DoesNotWaitForMigrationRunners()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(cancellationToken: TestContext.Current.CancellationToken);

        IResource storefrontWeb = builder.Resources.Single(resource => resource.Name == KnownNames.ResourceStorefrontWeb);

        Assert.Empty(storefrontWeb.Annotations.OfType<WaitAnnotation>());
    }

    [Fact]
    public async Task AppHost_RunMode_UsesKeycloakNotEntraExternalId()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(cancellationToken: TestContext.Current.CancellationToken);

        string[] resourceNames = [.. builder.Resources.Select(resource => resource.Name)];

        // ADR 0010/ADR 0020: Keycloak is local development's identity provider; Entra
        // External ID parameters (and the ACA environment they only matter alongside)
        // are publish-mode-only.
        Assert.Contains(KnownNames.ResourceKeycloak, resourceNames);
        Assert.DoesNotContain(KnownNames.ResourceEntraTenantSubdomain, resourceNames);
        Assert.DoesNotContain(KnownNames.ResourceEntraClientId, resourceNames);
        Assert.DoesNotContain(KnownNames.ResourceEntraClientSecret, resourceNames);
        Assert.DoesNotContain(KnownNames.ResourceStorefrontEntraClientId, resourceNames);
        Assert.DoesNotContain(KnownNames.ResourceStorefrontEntraClientSecret, resourceNames);
        Assert.DoesNotContain(KnownNames.ResourceAcaEnvironment, resourceNames);
    }

    [Fact]
    public async Task AppHost_PublishMode_OmitsDevTools()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(["--operation", "publish"], cancellationToken: TestContext.Current.CancellationToken);

        string[] resourceNames = [.. builder.Resources.Select(resource => resource.Name)];

        // Confirms this dev-only stand-in for Commerce Connect's Merchandiser review
        // never leaks into a real deployment manifest.
        Assert.DoesNotContain(KnownNames.ResourceDevTools, resourceNames);
    }

    [Fact]
    public async Task AppHost_PublishMode_UsesEntraExternalIdNotKeycloak()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(["--operation", "publish"], cancellationToken: TestContext.Current.CancellationToken);

        string[] resourceNames = [.. builder.Resources.Select(resource => resource.Name)];

        // ADR 0020 (amends ADR 0010): a deployed instance uses Microsoft Entra External
        // ID instead of Keycloak - no Keycloak resource, its admin parameters, or the
        // Bff's OIDC client secret parameter should exist once publishing.
        Assert.DoesNotContain(KnownNames.ResourceKeycloak, resourceNames);
        Assert.DoesNotContain(KnownNames.ResourceKeycloakAdminUsername, resourceNames);
        Assert.DoesNotContain(KnownNames.ResourceKeycloakAdminPassword, resourceNames);
        Assert.DoesNotContain(KnownNames.ResourceSellerPortalOidcClientSecret, resourceNames);
        Assert.DoesNotContain(KnownNames.ResourceStorefrontOidcClientSecret, resourceNames);
        Assert.Contains(KnownNames.ResourceEntraTenantSubdomain, resourceNames);
        Assert.Contains(KnownNames.ResourceEntraClientId, resourceNames);
        Assert.Contains(KnownNames.ResourceEntraClientSecret, resourceNames);
        Assert.Contains(KnownNames.ResourceStorefrontEntraClientId, resourceNames);
        Assert.Contains(KnownNames.ResourceStorefrontEntraClientSecret, resourceNames);
    }

    [Fact]
    public async Task AppHost_PublishMode_AddsAcaEnvironment()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(["--operation", "publish"], cancellationToken: TestContext.Current.CancellationToken);

        string[] resourceNames = [.. builder.Resources.Select(resource => resource.Name)];

        // ADR 0018: Azure Container Apps is the deployment target.
        Assert.Contains(KnownNames.ResourceAcaEnvironment, resourceNames);
    }
}
