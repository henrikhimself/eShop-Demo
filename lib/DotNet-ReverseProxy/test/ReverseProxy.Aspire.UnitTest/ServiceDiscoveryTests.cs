using Microsoft.Extensions.Configuration;

namespace Hj.ReverseProxy.Aspire.UnitTest;

public class ServiceDiscoveryTests
{
  [Fact]
  public void DiscoverEndpointList_GivenInvalidQuery_ReturnsEmpty()
  {
    // arrange
    var config = GetConfiguration([]);

    // act & assert
    Assert.Throws<InvalidOperationException>(() => ServiceDiscovery.DiscoverEndpointList(config, "invalid-query"));
  }

  [Fact]
  public void DiscoverEndpointList_GivenMissingServiceDiscoverySection_ReturnsEmpty()
  {
    // arrange
    var config = GetConfiguration([]);

    // act
    var result = ServiceDiscovery.DiscoverEndpointList(config, "https://service");

    // assert
    Assert.Empty(result);
  }

  [Fact]
  public void DiscoverEndpointList_GivenService_ReturnsEndpoints()
  {
    // arrange
    var config = GetConfiguration(new Dictionary<string, string>()
    {
      { "Services:Service:Endpoint:0", "http://localhost" },
      { "Services:Service:Endpoint:1", "https://localhost" },
    });

    // act
    var result = ServiceDiscovery.DiscoverEndpointList(config, "endpoint://service");

    // assert
    Assert.Equal("http://localhost", result[0]);
    Assert.Equal("https://localhost", result[1]);
  }

  [Fact]
  public void DiscoverEndpointList_GivenServiceAndNamedEndpoint_ReturnsNamedEndpoint()
  {
    // arrange
    var config = GetConfiguration(new Dictionary<string, string>()
    {
      { "Services:Service:Endpoint:0", "https://localhost" },
      { "Services:Service:Named-Endpoint:0", "https://named-endpoint" },
    });

    // act
    var result = ServiceDiscovery.DiscoverEndpointList(config, "https://_named-endpoint.service");

    // assert
    var item = Assert.Single(result);
    Assert.Equal("https://named-endpoint", item);
  }

  [Theory]
  [InlineData("http")]
  [InlineData("https")]
  public void DiscoverEndpointList_GivenServiceWithAllowedSchemes_ReturnsEndpointWithAllowedScheme(string allowedScheme)
  {
    // arrange
    var config = GetConfiguration(new Dictionary<string, string>()
    {
      { "Services:Service:Endpoint:0", "http://localhost" },
      { "Services:Service:Endpoint:1", "https://localhost" },
    });

    string[] allowedSchemes = [allowedScheme];

    // act
    var result = ServiceDiscovery.DiscoverEndpointList(config, "http+https://_endpoint.service", allowedSchemes, false);

    // assert
    var item = Assert.Single(result);
    Assert.Equal(allowedScheme + "://localhost", item);
  }

  [Theory]
  [InlineData("")]
  [InlineData("http+")]
  public void DiscoverEndpointList_GivenInvalidAllowedSchemes_SkipsAllowedSchemeReturnsNone(string allowedScheme)
  {
    // arrange
    var config = GetConfiguration(new Dictionary<string, string>()
    {
      { "Services:Service:Endpoint:0", "http://localhost" },
      { "Services:Service:Endpoint:1", "https://localhost" },
    });

    string[]? allowedSchemes = [allowedScheme];

    // act
    var result = ServiceDiscovery.DiscoverEndpointList(config, "http+https://_endpoint.service", allowedSchemes, false);

    // assert
    Assert.Empty(result);
  }

  [Fact]
  public void ReadConfiguration_GivenForwardedOriginEnabled_ReturnsMappingWithForwardedOrigin()
  {
    // arrange
    var config = GetConfiguration(new Dictionary<string, string>()
    {
      { "reverseproxy:website", "one.eshop.local" },
      { "reverseproxyforwardedorigin:website", "true" },
    });

    // act
    var mapping = Assert.Single(ServiceDiscovery.ReadConfiguration(config));

    // assert
    Assert.Equal("website", mapping.ServiceName);
    Assert.Equal("one.eshop.local", mapping.HostName);
    Assert.True(mapping.ForwardPublicOrigin);
  }

  [Fact]
  public void CreateRouteConfig_GivenForwardedOriginEnabled_ConfiguresProxyAuthoritativeForwardedHeaders()
  {
    // arrange & act
    var route = ServiceDiscoveryStartupFilter.CreateRouteConfig("website", "one.eshop.local", forwardPublicOrigin: true);

    // assert
    var transforms = route.Transforms;
    Assert.NotNull(transforms);

    var forwardedTransform = Assert.Single(transforms, transform => transform.ContainsKey("X-Forwarded"));
    Assert.Equal("Remove", forwardedTransform["X-Forwarded"]);
    Assert.Equal("Remove", forwardedTransform["For"]);
    Assert.Equal("Set", forwardedTransform["Host"]);
    Assert.Equal("Set", forwardedTransform["Proto"]);
    Assert.Equal("Remove", forwardedTransform["Prefix"]);

    var originalHostTransform = Assert.Single(transforms, transform => transform.ContainsKey("RequestHeaderOriginalHost"));
    Assert.Equal("False", originalHostTransform["RequestHeaderOriginalHost"]);
  }

  [Fact]
  public void CreateRouteConfig_GivenForwardedOriginDisabled_DoesNotConfigureForwardedHeaders()
  {
    // arrange & act
    var route = ServiceDiscoveryStartupFilter.CreateRouteConfig("website", "one.eshop.local", forwardPublicOrigin: false);

    // assert
    Assert.Null(route.Transforms);
  }

  private static IConfiguration GetConfiguration(Dictionary<string, string> settings)
  {
    var initialData = settings.Select(kvp => new KeyValuePair<string, string?>(kvp.Key, kvp.Value));
    return new ConfigurationBuilder().AddInMemoryCollection(initialData).Build();
  }
}
