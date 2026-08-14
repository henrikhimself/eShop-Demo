// <copyright file="Program.cs" company="Henrik Jensen">
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

using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Hj.EShop.Common;
using Hj.EShop.SellerPortal.Bff.Authentication;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Endpoints;
using Hj.EShop.SellerPortal.Bff.Messaging;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Hj.EShop.ServiceDefaults;
using Microsoft.AspNetCore.HttpOverrides;

// Build-time OpenAPI generation (`eshop generate types`) runs this entry point through
// a mock server, so registrations needing a real Aspire-provided connection are skipped
// below - see doc/adr/0014-bff-openapi-source-of-truth-for-frontend-types.md.
bool isBuildTimeOpenApiGeneration = EnvironmentChecks.IsBuildTimeOpenApiGeneration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// General-purpose infrastructure, not owned by any one feature - currently backs
// HybridCacheTicketStore (AuthConfiguration.cs), and will also back output caching.
builder.Services.AddHybridCache();

if (!isBuildTimeOpenApiGeneration)
{
    builder.AddServiceDefaults();
}

// The SellerPortalDbContext registration (real or, for build-time OpenAPI generation, a
// fake) - see DbContextConfiguration.cs.
builder.AddDbContextConfiguration(isBuildTimeOpenApiGeneration);

// Reports not-ready until EShop.SellerPortal.MigrationRunner's schema marker says the
// database is migrated - see SchemaMarkerHealthCheck. Skipped in "Testing" (SQLite, not
// a real SQL Server) and build-time OpenAPI generation, same as the DbContext above.
if (!builder.Environment.IsTestingEnvironment() && !isBuildTimeOpenApiGeneration)
{
    builder.Services.AddHealthChecks().AddCheck<SchemaMarkerHealthCheck>("schema-marker");
}

// Skipped in "Testing" (SellerPortalWebApplicationFactory registers its own
// SQLite-backed context with no real SQL Server/Service Bus/Storage) and during
// build-time OpenAPI generation, same as the AddServiceDefaults guard above.
if (!builder.Environment.IsTestingEnvironment() && !isBuildTimeOpenApiGeneration)
{
    builder.AddAzureServiceBusClient(connectionName: KnownNames.ResourceServiceBus);
    builder.AddAzureBlobContainerClient(connectionName: KnownNames.ResourceSellerSubmissionsImage);
    builder.AddRedisClient(connectionName: KnownNames.ResourceCache);
    builder.AddRedisDistributedCache(connectionName: KnownNames.ResourceCache);

    builder.Services.AddHostedService<SubmissionResultConsumer>();
    builder.Services.AddHostedService<SubmissionImageDeletionConsumer>();
    builder.Services.AddHostedService<InventoryResultConsumer>();
}
else if (isBuildTimeOpenApiGeneration)
{
    // AddServiceDefaults() is skipped above, but MapDefaultEndpoints() below always
    // maps /health and /alive unconditionally - needs AddHealthChecks() registered
    // regardless of this mock host run, or mapping throws.
    builder.AddDefaultHealthChecks();

    // Minimal API's binding inference needs each handler parameter to resolve as *some*
    // service or it treats it as a request body (breaks GETs); these two fakes only
    // need to exist, never connect to anything real.
    // Factory form (not AddSingleton(instance)) so the DI container disposes this
    // itself at shutdown - CA2000 otherwise flags the constructed-but-unowned instance.
    builder.Services.AddSingleton(_ =>
        new ServiceBusClient("Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=fake;SharedAccessKey=ZmFrZQ=="));
    builder.Services.AddSingleton(new BlobContainerClient("UseDevelopmentStorage=true", "fake"));
}

// Cookie/OIDC authentication, the "SellerOnly" authorization policy, and the
// Data Protection/ticket-store persistence backing the auth cookie - see
// AuthConfiguration.cs.
builder.AddAuthConfiguration(isBuildTimeOpenApiGeneration);

builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddSingleton<SubmissionNotificationBroadcaster>();
builder.Services.AddSingleton<InventoryNotificationBroadcaster>();

// The OIDC handler must compute redirect_uri from the forwarded host (every request
// arrives proxied through the Next.js app), so keep the default KnownNetworks/
// KnownProxies (loopback only) instead of clearing them - an empty list would let any
// client spoof X-Forwarded-Host.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapAuthEndpoints();
app.MapAntiforgeryEndpoints();
app.MapDraftEndpoints();
app.MapDraftImageEndpoints();
app.MapMerchandiseDraftEndpoints();
app.MapMerchandiseImageEndpoints();
app.MapSubmissionEndpoints();
app.MapSubmissionEventsEndpoints();
app.MapInventoryEndpoints();
app.MapInventoryEventsEndpoints();

await app.RunAsync();
