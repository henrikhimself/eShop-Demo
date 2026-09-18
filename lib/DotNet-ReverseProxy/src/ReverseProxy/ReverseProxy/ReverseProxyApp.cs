// <copyright file="ReverseProxyApp.cs" company="Henrik Jensen">
// Copyright 2025 Henrik Jensen
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

using Yarp.ReverseProxy.Configuration;

namespace Hj.ReverseProxy.ReverseProxy;

internal sealed class ReverseProxyApp(
  IConfigValidator configValidator,
  InMemoryConfigProvider inMemoryConfigProvider,
  IEnumerable<IProxyConfigProvider> proxyConfigProviders)
{
  private readonly RouteConfig _blackholeRoute = new()
  {
    RouteId = ReverseProxyConstants.BlackholeId,
    ClusterId = ReverseProxyConstants.BlackholeId,
    Order = int.MaxValue,
    Match = new() { Path = "/{**catch-all}", },
  };

  private readonly ClusterConfig _blackholeCluster = new()
  {
    ClusterId = ReverseProxyConstants.BlackholeId,
    Destinations = new Dictionary<string, DestinationConfig>()
    {
      { ReverseProxyConstants.BlackholeId, new() { Address = "https:///", } },
    },
  };

  private readonly List<RouteConfig> _routes = [];
  private readonly List<ClusterConfig> _clusters = [];

  public IReadOnlyList<RouteConfig> GetRouteConfigs() => proxyConfigProviders.SelectMany(x => x.GetConfig().Routes).ToList().AsReadOnly();

  public IReadOnlyList<ClusterConfig> GetClusterConfigs() => proxyConfigProviders.SelectMany(x => x.GetConfig().Clusters).ToList().AsReadOnly();

  public void AddBlackholeCatchAll()
  {
    _routes.Add(_blackholeRoute);
    _clusters.Add(_blackholeCluster);
    Update();
  }

  public async ValueTask AddRouteAsync(RouteConfig route, bool allowOverwrite)
  {
    var validationErrors = await configValidator.ValidateRouteAsync(route);
    if (validationErrors.Count > 0)
    {
      throw new AggregateException("Could not add route.", validationErrors);
    }

    var hasExisting = proxyConfigProviders.Any(x => x.GetConfig().Routes.Any(y => y.RouteId == route.RouteId));
    if (hasExisting && !allowOverwrite)
    {
      throw new InvalidOperationException($"Route with id '{route.RouteId}' already exists.");
    }

    if (hasExisting)
    {
      _routes.RemoveAll(x => x.RouteId == route.RouteId);
    }

    _routes.Add(route);
    Update();
  }

  public async ValueTask AddClusterAsync(ClusterConfig cluster, bool allowOverwrite)
  {
    var validationErrors = await configValidator.ValidateClusterAsync(cluster);
    if (validationErrors.Count > 0)
    {
      throw new AggregateException("Could not add cluster.", validationErrors);
    }

    var hasExisting = proxyConfigProviders.Any(x => x.GetConfig().Clusters.Any(y => y.ClusterId == cluster.ClusterId));
    if (hasExisting && !allowOverwrite)
    {
      throw new InvalidOperationException($"Cluster with id '{cluster.ClusterId}' already exists.");
    }

    if (hasExisting)
    {
      _clusters.RemoveAll(x => x.ClusterId == cluster.ClusterId);
    }

    _clusters.Add(cluster);
    Update();
  }

  public void Update() => inMemoryConfigProvider.Update(_routes.AsReadOnly(), _clusters.AsReadOnly());
}
