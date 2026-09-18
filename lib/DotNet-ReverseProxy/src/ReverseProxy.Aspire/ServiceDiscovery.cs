// <copyright file="ServiceDiscovery.cs" company="Henrik Jensen">
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

using Microsoft.Extensions.Configuration;

namespace Hj.ReverseProxy.Aspire;

internal static class ServiceDiscovery
{
  public static IEnumerable<(string ServiceName, string HostName, bool ForwardPublicOrigin)> ReadConfiguration(IConfiguration configuration)
  {
    var reverseProxySection = configuration.GetSection(Constants.EnvPrefix);
    foreach (var hostMapping in reverseProxySection.GetChildren())
    {
      var serviceName = hostMapping.Key;
      var hostName = hostMapping.Value;
      if (serviceName is null || hostName is null)
      {
        continue;
      }

      var forwardPublicOrigin = configuration.GetValue<bool>($"{Constants.ForwardedOriginEnvPrefix}:{serviceName}");
      yield return (serviceName, hostName, forwardPublicOrigin);
    }
  }

  public static string[] DiscoverEndpointList(IConfiguration configuration, string query, string[]? allowedSchemes = null, bool allowAllSchemes = true)
  {
    if (!Uri.TryCreate(query, UriKind.Absolute, out var uri))
    {
      throw new InvalidOperationException($"Invalid service discovery query '{query}'");
    }

    var serviceName = uri.Host;
    var namedEndpoint = string.Empty;
    var namedEndpointSeparator = serviceName.IndexOf('.', StringComparison.Ordinal);
    if (serviceName[0] == '_' && namedEndpointSeparator > 1 && serviceName[^1] != '.')
    {
      namedEndpoint = serviceName[1..namedEndpointSeparator];
      serviceName = serviceName[(namedEndpointSeparator + 1)..];
    }

    allowedSchemes ??= uri.Scheme.Split('+');

    ReadOnlySpan<string> endpoints = [namedEndpoint, .. allowedSchemes];
    foreach (var endpoint in endpoints)
    {
      var section = configuration.GetSection($"Services:{serviceName}:{endpoint}");
      if (section.Exists())
      {
        var uriStrings = section.Get<string[]>();
        if (uriStrings is null || allowAllSchemes)
        {
          return uriStrings ?? [];
        }

        uriStrings = [.. uriStrings.Where(x =>
        {
          return Uri.TryCreate(x, default, out var uri)
            && allowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
        })];
        return uriStrings;
      }
    }

    return [];
  }
}
