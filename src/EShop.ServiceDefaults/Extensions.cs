// <copyright file="Extensions.cs" company="Henrik Jensen">
// Copyright 2026 Henrik Jensen
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
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Hj.EShop.ServiceDefaults;

public static class Extensions
{
    // See doc/MEMORY.md — StoreFront uses the Startup.cs overload below instead of this minimal-hosting one.
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Do not also call ConfigureOpenTelemetry() here - it would duplicate ConfigureOpenTelemetryServices and could register UseOtlpExporter twice.
        builder.Logging.ConfigureOpenTelemetryLogging();

        AddServiceDefaults(builder.Services, builder.Configuration, builder.Environment.ApplicationName, builder.Environment);

        return builder;
    }

    // Startup.cs hosting: call from ConfigureServices; call ConfigureOpenTelemetryLogging separately from Program.cs.
    public static void AddServiceDefaults(IServiceCollection services, IConfiguration configuration, string applicationName, IHostEnvironment environment)
    {
        ConfigureOpenTelemetryServices(services, configuration, applicationName);

        AddDefaultHealthChecks(services);

        services.AddServiceDiscovery();

        // Enriches every resilience pipeline's telemetry, not just the HttpClient one below (e.g. EShop.Messaging's ServiceBusQueueConsumer retry pipeline).
        services.AddResilienceEnricher();

        services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();

            http.AddServiceDiscovery();

            // Every HttpClientFactory client needs the OIDC handler's relaxed TLS validation too (see TestingDefaults).
            // Gated on non-Production, not IsFakeEnvironment(): Aspire launches Bff/Storefront under "Development" regardless of native vs. containerized e2e runs.
            if (!environment.IsProduction())
            {
                http.ConfigurePrimaryHttpMessageHandler(TestingDefaults.CreateLenientHttpHandler);
            }
        });
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.ConfigureOpenTelemetryLogging();

        ConfigureOpenTelemetryServices(builder.Services, builder.Configuration, builder.Environment.ApplicationName);

        return builder;
    }

    // Split for Startup.cs hosting, which does not expose Logging and Services together
    // like IHostApplicationBuilder.
    public static void ConfigureOpenTelemetryLogging(this ILoggingBuilder logging)
    {
        logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        AddDefaultHealthChecks(builder.Services);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Centralized here; these middlewares only affect endpoints opting in below.
        app.UseRequestTimeouts();
        app.UseOutputCache();

        app.MapHealthChecks("/health")
            .CacheOutput("HealthChecks")
            .WithRequestTimeout("HealthChecks");

        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
        })
            .CacheOutput("HealthChecks")
            .WithRequestTimeout("HealthChecks");

        return app;
    }

    // Startup.cs hosting uses separate objects for middleware and endpoint mapping.
    // Call this inside the project's single UseEndpoints block.
    public static IEndpointRouteBuilder MapDefaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health")
            .CacheOutput("HealthChecks")
            .WithRequestTimeout("HealthChecks");

        endpoints.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
        })
            .CacheOutput("HealthChecks")
            .WithRequestTimeout("HealthChecks");

        return endpoints;
    }

    // Startup.cs hosting: call before UseEndpoints.
    public static IApplicationBuilder UseDefaultEndpointsMiddleware(this IApplicationBuilder app)
    {
        app.UseRequestTimeouts();
        app.UseOutputCache();

        return app;
    }

    private static void AddDefaultHealthChecks(IServiceCollection services)
    {
        // ADR 0018: harden health endpoints for non-Development exposure.
        services.AddRequestTimeouts(
            configure: static timeouts => timeouts.AddPolicy("HealthChecks", TimeSpan.FromSeconds(5)));

        services.AddOutputCache(
            configureOptions: static caching => caching.AddPolicy(
                "HealthChecks",
                build: static policy => policy.Expire(TimeSpan.FromSeconds(10))));

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
    }

    private static void ConfigureOpenTelemetryServices(IServiceCollection services, IConfiguration configuration, string applicationName)
    {
        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(applicationName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        bool useOtlpExporter = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (useOtlpExporter)
        {
            services.AddOpenTelemetry().UseOtlpExporter();
        }
    }
}
