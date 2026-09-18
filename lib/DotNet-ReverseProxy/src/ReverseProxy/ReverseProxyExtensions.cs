// <copyright file="ReverseProxyExtensions.cs" company="Henrik Jensen">
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

using Hj.ReverseProxy.Abstraction;
using Hj.ReverseProxy.Certificate;
using Hj.ReverseProxy.Certificate.Strategy;
using Hj.ReverseProxy.ReverseProxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hj.ReverseProxy;

public static class ReverseProxyExtensions
{
  /// <summary>
  /// Configures the Kestrel server to use automatic self-signed certificate generation for HTTPS connections.
  /// </summary>
  /// <param name="options">The <see cref="KestrelServerOptions"/> instance to configure.</param>
  public static void UseSelfSignedCertificate(this KestrelServerOptions options)
  {
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
      httpsOptions.ServerCertificateSelector = (context, hostName) =>
      {
        if (hostName is null)
        {
          return null;
        }

        using var scope = options.ApplicationServices.CreateScope();
        var certificateApp = scope.ServiceProvider.GetRequiredService<CertificateApp>();
        return certificateApp.GetCertificate(hostName);
      };
    });
  }

  /// <summary>
  /// Configures reverse proxy services for the application.
  /// </summary>
  /// <param name="services">The <see cref="IServiceCollection"/> to which the reverse proxy services will be added.</param>
  /// <param name="configuration">The <see cref="IConfiguration"/> instance containing an optional reverse proxy configuration.</param>
  /// <returns>The updated <see cref="IServiceCollection"/> with reverse proxy services configured.</returns>
  public static IServiceCollection ConfigureReverseProxy(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddSingleton<ReverseProxyApp>();

    services.AddHttpLogging(options =>
    {
      options.LoggingFields = HttpLoggingFields.RequestPath | HttpLoggingFields.RequestQuery;
      options.RequestBodyLogLimit = int.MaxValue;
      options.ResponseBodyLogLimit = int.MaxValue;
    });

    services.AddSingleton<LoggerMiddleware>();

    services.AddReverseProxy()
      .LoadFromMemory([], [])
      .LoadFromConfig(configuration.GetSection("ReverseProxy"))
      .AddTransforms(builderContext =>
      {
        builderContext.CopyRequestHeaders = true;
        builderContext.CopyResponseHeaders = true;
      });

    services.AddMemoryCache();
    services.TryAddSingleton<IFileStore, FileSystemStore>();
    services.TryAddSingleton<ICertificateConfig, CertificateConfig>();
    services.AddSingleton<ICertificateStrategy, CertificateRsa>();
    services.AddSingleton<ICertificateStrategy, CertificateEcdsa>();

    // Register appropriate CA loader strategy based on platform and .NET version
#if NET8_0
    if (OperatingSystem.IsMacOS())
    {
      services.AddSingleton<ICaLoader, MacOSNet8CaLoader>();
      services.AddSingleton<ICertificateCreator, MacOSNet8CertificateCreator>();
    }
    else
    {
      services.AddSingleton<ICaLoader, DefaultCaLoader>();
      services.AddSingleton<ICertificateCreator, DefaultCertificateCreator>();
    }
#else
    services.AddSingleton<ICaLoader, DefaultCaLoader>();
    services.AddSingleton<ICertificateCreator, DefaultCertificateCreator>();
#endif

    services.AddSingleton<CertificateStore>();
    services.AddSingleton<CertificateFactory>();
    services.AddSingleton<CertificateApp>();

    return services;
  }

  /// <summary>
  /// Configures the application to use the reverse proxy.
  /// </summary>
  /// <param name="app">The <see cref="WebApplication"/> instance to configure.</param>
  /// <returns>The configured <see cref="WebApplication"/> instance.</returns>
  public static WebApplication UseReverseProxy(this WebApplication app)
  {
    var loggerMiddleware = app.Services.GetRequiredService<LoggerMiddleware>();

    app.MapReverseProxy(proxyPipeline =>
    {
      proxyPipeline.UseHttpLogging();
      proxyPipeline.Use(async (context, next) => await loggerMiddleware.InvokeAsync(context, next));
    });

    return app;
  }

  /// <summary>
  /// Configures the application to enable the Reverse Proxy API with an optional route prefix.
  /// </summary>
  /// <param name="app">The <see cref="WebApplication"/> instance to configure.</param>
  /// <param name="routePrefix">An optional route prefix for the Reverse Proxy API. If <see langword="null"/> or empty, the API will be mapped to
  /// the root route.</param>
  /// <returns>The configured <see cref="WebApplication"/> instance.</returns>
  public static WebApplication UseReverseProxyApi(this WebApplication app, string? routePrefix = null)
  {
    app.MapApi(routePrefix);
    return app;
  }

  /// <summary>
  /// Configures the application to handle all unmatched requests with a "blackhole" catch-all route.
  /// </summary>
  /// <param name="app">The <see cref="WebApplication"/> instance to configure.</param>
  /// <returns>The configured <see cref="WebApplication"/> instance, allowing for further chaining of calls.</returns>
  public static WebApplication UseBlackholeCatchAll(this WebApplication app)
  {
    var reverseProxyApp = app.Services.GetRequiredService<ReverseProxyApp>();
    reverseProxyApp.AddBlackholeCatchAll();
    return app;
  }
}
