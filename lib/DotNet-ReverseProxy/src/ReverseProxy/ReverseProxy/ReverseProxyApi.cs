// <copyright file="ReverseProxyApi.cs" company="Henrik Jensen">
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

using Hj.ReverseProxy.ReverseProxy.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Hj.ReverseProxy.ReverseProxy;

internal static class ReverseProxyApi
{
  public static IEndpointRouteBuilder MapApi(this IEndpointRouteBuilder app, string? routePrefix)
  {
    var api = app;
    if (!string.IsNullOrWhiteSpace(routePrefix))
    {
      api = app.MapGroup(routePrefix);
    }

    api.MapGet("/route", GetRoute)
      .WithName("GetRoutes")
      .WithDescription("Get list of configured routes.");
    api.MapPost("/route", PostRouteAsync)
      .WithName("AddRoute")
      .WithDescription("Appends routes to the configuration.");

    api.MapGet("/cluster", GetCluster)
      .WithName("GetClusters")
      .WithDescription("Get list of configured clusters.");
    api.MapPost("/cluster", PostClusterAsync)
      .WithName("AddCluster")
      .WithDescription("Appends clusters to the configuration.");

    return app;
  }

  public static IResult GetRoute([FromServices] ReverseProxyApp reverseProxyApp) => Results.Json(reverseProxyApp.GetRouteConfigs());

  public static async ValueTask<IResult> PostRouteAsync([FromServices] ReverseProxyApp reverseProxyApp, [FromBody] RouteInputDto routeInput)
  {
    if (routeInput.Routes is not null)
    {
      foreach (var routeConfig in routeInput.Routes)
      {
        await reverseProxyApp.AddRouteAsync(routeConfig, routeInput.AllowOverwrite);
      }
    }

    return Results.Ok();
  }

  public static IResult GetCluster([FromServices] ReverseProxyApp reverseProxyApp) => Results.Json(reverseProxyApp.GetClusterConfigs());

  public static async ValueTask<IResult> PostClusterAsync([FromServices] ReverseProxyApp reverseProxyApp, [FromBody] ClusterInputDto clusterInput)
  {
    if (clusterInput.Clusters is not null)
    {
      foreach (var clusterConfig in clusterInput.Clusters)
      {
        await reverseProxyApp.AddClusterAsync(clusterConfig, clusterInput.AllowOverwrite);
      }
    }

    return Results.Ok();
  }
}
