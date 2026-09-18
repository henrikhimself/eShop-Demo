using Aspire.Hosting;
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
        Assert.Contains(KnownNames.ResourceDevReverseProxy, resourceNames);
    }

    [Fact]
    public async Task AppHost_PublishMode_OmitsReverseProxy()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(["--operation", "publish"], cancellationToken: TestContext.Current.CancellationToken);

        string[] resourceNames = [.. builder.Resources.Select(resource => resource.Name)];

        // PLAN-2.md §3/§6.1: the local reverse proxy is dev-only - Azure Container Apps
        // ingress is the production browser edge.
        Assert.DoesNotContain(KnownNames.ResourceDevReverseProxy, resourceNames);
    }

    [Fact]
    public async Task AppHost_RunMode_SellerPortalWebAndStorefrontWebHaveNoExternalHttpEndpoint()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(cancellationToken: TestContext.Current.CancellationToken);

        // PLAN-2.md §3: the reverse proxy is now the only local browser ingress for
        // these two resources - neither should call WithExternalHttpEndpoints() itself
        // in run mode any more.
        IResource sellerPortalWeb = builder.Resources.Single(resource => resource.Name == KnownNames.ResourceSellerPortalWeb);
        IResource storefrontWeb = builder.Resources.Single(resource => resource.Name == KnownNames.ResourceStorefrontWeb);

        Assert.All(sellerPortalWeb.Annotations.OfType<EndpointAnnotation>(), endpoint => Assert.False(endpoint.IsExternal));
        Assert.All(storefrontWeb.Annotations.OfType<EndpointAnnotation>(), endpoint => Assert.False(endpoint.IsExternal));
    }

    [Fact]
    public async Task AppHost_ReverseProxy_HasFixedHttps8443Endpoint()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(cancellationToken: TestContext.Current.CancellationToken);

        IResource reverseProxy = builder.Resources.Single(resource => resource.Name == KnownNames.ResourceDevReverseProxy);
        EndpointAnnotation httpsEndpoint = reverseProxy.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "https");

        Assert.Equal(KnownNames.ReverseProxyHttpsPort, httpsEndpoint.Port);
    }

    [Fact]
    public async Task AppHost_ReverseProxy_RoutesKeycloakSellerPortalAndStorefrontHostNames()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(cancellationToken: TestContext.Current.CancellationToken);

        await using DistributedApplication app = await builder.BuildAsync(TestContext.Current.CancellationToken);
        IResource reverseProxy = builder.Resources.Single(resource => resource.Name == KnownNames.ResourceDevReverseProxy);

        IExecutionConfigurationResult configuration = await ExecutionConfigurationBuilder
            .Create(reverseProxy)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(builder.ExecutionContext, cancellationToken: TestContext.Current.CancellationToken);

        // "reverseproxy"/"reverseproxyforwardedorigin" are Hj.ReverseProxy.Aspire's own
        // internal env var prefixes (WithReverseProxyReference, ResourceBuilderExtensions.cs)
        // - a fixed external contract the package reads verbatim, not something eShop
        // code centralizes a constant for.
        AssertRoutesTo(configuration, KnownNames.ResourceKeycloak, KnownNames.ReverseProxyIdentityHostName);
        AssertRoutesTo(configuration, KnownNames.ResourceSellerPortalWeb, KnownNames.ReverseProxySellerPortalHostName);
        AssertRoutesTo(configuration, KnownNames.ResourceStorefrontWeb, KnownNames.ReverseProxyStorefrontHostName);

        static void AssertRoutesTo(IExecutionConfigurationResult configuration, string targetResourceName, string expectedHostName)
        {
            Assert.Contains(
                configuration.EnvironmentVariables,
                pair => pair.Key == $"reverseproxy__{targetResourceName}" && pair.Value == expectedHostName);
            Assert.Contains(
                configuration.EnvironmentVariables,
                pair => pair.Key == $"reverseproxyforwardedorigin__{targetResourceName}" && pair.Value == bool.TrueString);
        }
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
    public async Task AppHost_ReverseProxy_HasReverseProxyHomeEnvironmentVariable()
    {
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.EShop_AppHost>(cancellationToken: TestContext.Current.CancellationToken);

        await using DistributedApplication app = await builder.BuildAsync(TestContext.Current.CancellationToken);
        IResource reverseProxy = builder.Resources.Single(resource => resource.Name == KnownNames.ResourceDevReverseProxy);

        IExecutionConfigurationResult configuration = await ExecutionConfigurationBuilder
            .Create(reverseProxy)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(builder.ExecutionContext, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(
            configuration.EnvironmentVariables,
            pair => pair.Key == KnownNames.ReverseProxyHomeEnvVarName && !string.IsNullOrWhiteSpace(pair.Value));
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
