// <copyright file="LoggerMiddleware.cs" company="Henrik Jensen">
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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Hj.ReverseProxy.ReverseProxy;

internal sealed class LoggerMiddleware(ILogger<LoggerMiddleware> logger)
{
  public async Task InvokeAsync(HttpContext context, RequestDelegate next)
  {
    var proxyFeature = context.GetReverseProxyFeature();
    var route = proxyFeature.Route.Config;

    if (string.Equals(ReverseProxyConstants.BlackholeId, route.RouteId, StringComparison.OrdinalIgnoreCase))
    {
      if (logger.IsEnabled(LogLevel.Debug))
      {
        logger.LogDebug("Blackhole route: '{Url}', unknown route", context.Request.GetDisplayUrl());
      }

      context.Response.StatusCode = (int)HttpStatusCode.NotFound;
      await context.Response.CompleteAsync();
      return;
    }

    if (logger.IsEnabled(LogLevel.Debug))
    {
      logger.LogDebug("Route '{RouteId}', match '{Match}', cluster '{ClusterId}'", route.RouteId, route.Match.Path, route.ClusterId);
    }

    await next(context);
  }
}
