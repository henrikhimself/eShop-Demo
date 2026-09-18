using Hj.ReverseProxy.ReverseProxy;
using Yarp.ReverseProxy.Configuration;

namespace Hj.ReverseProxy.UnitTest.ReverseProxy;

public class ReverseProxyAppTests
{
  [Fact]
  public void AddBlackholeCatchAll__AddsBlackhole()
  {
    // arrange
    InMemoryConfigProvider? inMemoryConfig = null;

    var sut = SystemUnderTest.For<ReverseProxyApp>(arrange =>
    {
      SetHappyPath(arrange);
      inMemoryConfig = arrange.Instance<InMemoryConfigProvider>();
    });

    // act
    sut.AddBlackholeCatchAll();

    // assert
    var config = inMemoryConfig!.GetConfig();

    var route0 = config.Routes[0];
    Assert.Equal(ReverseProxyConstants.BlackholeId, route0.RouteId);

    var cluster0 = config.Clusters[0];
    Assert.Equal(ReverseProxyConstants.BlackholeId, cluster0.ClusterId);
  }

  [Fact]
  public async Task AddRouteAsync_GivenValidationError_ThrowsAsync()
  {
    // arrange
    var sut = SystemUnderTest.For<ReverseProxyApp>(arrange =>
    {
      SetHappyPath(arrange);

      arrange.Instance<IConfigValidator>()
        .ValidateRouteAsync(Arg.Any<RouteConfig>())
        .Returns([new InvalidOperationException()]);
    });

    RouteConfig routeConfig = new();

    // act & assert
    await Assert.ThrowsAnyAsync<AggregateException>(async () => await sut.AddRouteAsync(routeConfig, false));
  }

  [Fact]
  public async Task AddRouteAsync_GivenExisting_ThrowsAsync()
  {
    // arrange
    RouteConfig routeConfig = new() { RouteId = Guid.NewGuid().ToString() };
    var sut = SystemUnderTest.For<ReverseProxyApp>(arrange =>
    {
      SetHappyPath(arrange);

      arrange.Instance<InMemoryConfigProvider>()
        .Update([routeConfig], []);
    });

    // act & assert
    await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await sut.AddRouteAsync(routeConfig, false));
  }

  [Fact]
  public async Task AddRouteAsync_GivenExistingAndAllowOverwrite_UpdatesAsync()
  {
    // arrange
    var routeId = Guid.NewGuid().ToString();

    var sut = SystemUnderTest.For<ReverseProxyApp>(arrange =>
    {
      SetHappyPath(arrange);

      RouteConfig oldRouteConfig = new()
      {
        RouteId = routeId,
        ClusterId = "oldCluster",
      };

      arrange.Instance<InMemoryConfigProvider>()
        .Update([oldRouteConfig], []);
    });

    RouteConfig newRouteConfig = new()
    {
      RouteId = routeId,
      ClusterId = "newCluster",
    };

    // act
    await sut.AddRouteAsync(newRouteConfig, true);

    // assert
    var routeConfig = sut.GetRouteConfigs().Single(x => x.RouteId == routeId);
    Assert.Equal("newCluster", routeConfig.ClusterId);
  }

  [Fact]
  public async Task AddClusterAsync_GivenValidationError_ThrowsAsync()
  {
    // arrange
    var sut = SystemUnderTest.For<ReverseProxyApp>(arrange =>
    {
      SetHappyPath(arrange);

      arrange.Instance<IConfigValidator>()
        .ValidateClusterAsync(Arg.Any<ClusterConfig>())
        .Returns([new InvalidOperationException()]);
    });

    ClusterConfig clusterConfig = new();

    // act & assert
    await Assert.ThrowsAnyAsync<AggregateException>(async () => await sut.AddClusterAsync(clusterConfig, false));
  }

  [Fact]
  public async Task AddClusterAsync_GivenExisting_ThrowsAsync()
  {
    // arrange
    ClusterConfig clusterConfig = new() { ClusterId = Guid.NewGuid().ToString() };
    var sut = SystemUnderTest.For<ReverseProxyApp>(arrange =>
    {
      SetHappyPath(arrange);

      arrange.Instance<InMemoryConfigProvider>()
        .Update([], [clusterConfig]);
    });

    // act & assert
    await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await sut.AddClusterAsync(clusterConfig, false));
  }

  [Fact]
  public async Task AddClusterAsync_GivenExistingAndAllowOverwrite_UpdatesAsync()
  {
    // arrange
    var clusterId = Guid.NewGuid().ToString();

    var sut = SystemUnderTest.For<ReverseProxyApp>(arrange =>
    {
      SetHappyPath(arrange);

      ClusterConfig oldClusterConfig = new()
      {
        ClusterId = clusterId,
        Destinations = new Dictionary<string, DestinationConfig>()
        {
          { "destination1", new() { Address = "https://example.com/old", } },
        },
      };

      arrange.Instance<InMemoryConfigProvider>()
        .Update([], [oldClusterConfig]);
    });

    ClusterConfig newClusterConfig = new()
    {
      ClusterId = clusterId,
      Destinations = new Dictionary<string, DestinationConfig>()
      {
        { "destination1", new() { Address = "https://example.com/new", } },
      },
    };

    // act
    await sut.AddClusterAsync(newClusterConfig, true);

    // assert
    var clusterConfig = sut.GetClusterConfigs().Single(x => x.ClusterId == clusterId);
    Assert.Equal("https://example.com/new", clusterConfig.Destinations?["destination1"].Address);
  }

  private static void SetHappyPath(InputBuilder inputBuilder)
  {
    inputBuilder.Advanced.Instance(() => new InMemoryConfigProvider([], []));

    var configValidator = inputBuilder.Instance<IConfigValidator>();
    configValidator.ValidateRouteAsync(Arg.Any<RouteConfig>()).Returns([]);
    configValidator.ValidateClusterAsync(Arg.Any<ClusterConfig>()).Returns([]);
  }
}
