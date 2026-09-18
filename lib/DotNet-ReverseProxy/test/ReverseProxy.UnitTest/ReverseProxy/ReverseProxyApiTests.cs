using Hj.ReverseProxy.ReverseProxy;
using Hj.ReverseProxy.ReverseProxy.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Yarp.ReverseProxy.Configuration;

namespace Hj.ReverseProxy.UnitTest.ReverseProxy;

public class ReverseProxyApiTests
{
  [Fact]
  public void GetRoute__ReturnsRoute()
  {
    // arrange
    SutBuilder sutBuilder = new();
    var inputBuilder = SetHappyPath(sutBuilder.InputBuilder);

    RouteConfig routeConfig = new() { RouteId = Guid.NewGuid().ToString(), };

    inputBuilder.Instance<InMemoryConfigProvider>()
      .Update([routeConfig], []);

    var reverseProxyApp = inputBuilder.Instance<ReverseProxyApp>();

    // act
    var result = ReverseProxyApi.GetRoute(reverseProxyApp) as JsonHttpResult<IReadOnlyList<RouteConfig>>;

    // assert
    Assert.NotNull(result);
    Assert.Equal(routeConfig.RouteId, result?.Value?[0].RouteId);
  }

  [Fact]
  public async Task PostRoute_GivenRouteInput_AddsRouteAsync()
  {
    // arrange
    SutBuilder sutBuilder = new();
    var inputBuilder = SetHappyPath(sutBuilder.InputBuilder);

    var inMemoryConfig = inputBuilder.Instance<InMemoryConfigProvider>();
    var reverseProxyApp = inputBuilder.Instance<ReverseProxyApp>();

    var routeId = Guid.NewGuid().ToString();
    RouteInputDto routeInput = new()
    {
      Routes = [new() { RouteId = routeId, }],
    };

    // act
    var result = await ReverseProxyApi.PostRouteAsync(reverseProxyApp, routeInput) as Ok;

    // assert
    Assert.NotNull(result);
    Assert.Equal(routeId, inMemoryConfig.GetConfig().Routes[0].RouteId);
  }

  [Fact]
  public void GetCluster__ReturnsCluster()
  {
    // arrange
    SutBuilder sutBuilder = new();
    var inputBuilder = SetHappyPath(sutBuilder.InputBuilder);

    ClusterConfig clusterConfig = new() { ClusterId = Guid.NewGuid().ToString() };

    inputBuilder.Instance<InMemoryConfigProvider>()
      .Update([], [clusterConfig]);

    var reverseProxyApp = inputBuilder.Instance<ReverseProxyApp>();

    // act
    var result = ReverseProxyApi.GetCluster(reverseProxyApp) as JsonHttpResult<IReadOnlyList<ClusterConfig>>;

    // assert
    Assert.NotNull(result);
    Assert.Equal(clusterConfig.ClusterId, result?.Value?[0].ClusterId);
  }

  [Fact]
  public async Task PostCluster_GivenClusterInput_AddsClusterAsync()
  {
    // arrange
    SutBuilder sutBuilder = new();
    var inputBuilder = SetHappyPath(sutBuilder.InputBuilder);

    var inMemoryConfig = inputBuilder.Instance<InMemoryConfigProvider>();
    var reverseProxyApp = inputBuilder.Instance<ReverseProxyApp>();

    var clusterId = Guid.NewGuid().ToString();
    ClusterInputDto clusterInput = new()
    {
      Clusters = [new() { ClusterId = clusterId, }],
    };

    // act
    var result = await ReverseProxyApi.PostClusterAsync(reverseProxyApp, clusterInput) as Ok;

    // assert
    Assert.NotNull(result);
    Assert.Equal(clusterId, inMemoryConfig!.GetConfig().Clusters[0].ClusterId);
  }

  private static InputBuilder SetHappyPath(InputBuilder arrange)
  {
    arrange.Advanced.Instance(() => new InMemoryConfigProvider([], []));

    var configValidator = arrange.Instance<IConfigValidator>();
    configValidator.ValidateRouteAsync(Arg.Any<RouteConfig>()).Returns([]);
    configValidator.ValidateClusterAsync(Arg.Any<ClusterConfig>()).Returns([]);

    return arrange;
  }
}
