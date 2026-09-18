// <copyright file="ServiceDiscoveryStartupFilter.cs" company="Henrik Jensen">
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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Hj.ReverseProxy.Aspire;

public sealed class ServiceDiscoveryStartupFilter : IStartupFilter
{
  public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
  {
    return app =>
    {
      using var scope = app.ApplicationServices.CreateScope();
      var serviceProvider = scope.ServiceProvider;
      ConfigureYarp(
        serviceProvider.GetRequiredService<IConfiguration>(),
        serviceProvider.GetRequiredService<InMemoryConfigProvider>());

      next(app);
    };
  }

  internal static RouteConfig CreateRouteConfig(string serviceName, string hostName, bool forwardPublicOrigin)
  {
    var route = new RouteConfig()
    {
      RouteId = serviceName,
      ClusterId = serviceName,
      Match = new()
      {
        Hosts = [hostName],
        Path = "/{**catch-all}",
      },
    };

    if (!forwardPublicOrigin)
    {
      return route;
    }

    return route
      .WithTransformXForwarded(
        xDefault: ForwardedTransformActions.Remove,
        xFor: ForwardedTransformActions.Remove,
        xHost: ForwardedTransformActions.Set,
        xProto: ForwardedTransformActions.Set,
        xPrefix: ForwardedTransformActions.Remove)
      .WithTransformUseOriginalHostHeader(useOriginal: false);
  }

  private static void ConfigureYarp(
    IConfiguration configuration,
    InMemoryConfigProvider inMemoryConfigProvider)
  {
    List<RouteConfig> routes = [];
    List<ClusterConfig> clusters = [];

    var hostMappings = ServiceDiscovery.ReadConfiguration(configuration);

    foreach ((var serviceName, var hostName, var forwardPublicOrigin) in hostMappings)
    {
      var endpoints = ServiceDiscovery.DiscoverEndpointList(configuration, "https+http://" + serviceName);
      if (endpoints.Length == 0)
      {
        continue;
      }

      var destinations = new Dictionary<string, DestinationConfig>();
      for (var i = 0; i < endpoints.Length; i++)
      {
        destinations.Add("destination" + i, new DestinationConfig() { Address = endpoints[i], });
      }

      clusters.Add(new ClusterConfig()
      {
        ClusterId = serviceName,
        Destinations = destinations,
      });

      routes.Add(CreateRouteConfig(serviceName, hostName, forwardPublicOrigin));
    }

    inMemoryConfigProvider.Update(routes, clusters);
  }
}
