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

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// General-purpose infrastructure, not owned by any one feature - currently backs
// HybridCacheTicketStore (AuthConfiguration.cs), and will also back output caching.
builder.Services.AddHybridCache();

// Build-time OpenAPI generation (`eshop generate types`) runs this entry point through
// a mock server, so registrations needing a real Aspire-provided connection are skipped
// below - see doc/adr/0014-bff-openapi-source-of-truth-for-frontend-types.md.
if (!EnvironmentChecks.IsBuildTimeOpenApiGeneration())
{
    builder.AddServiceDefaults();
}

// The SellerPortalDbContext registration (real or, for build-time OpenAPI generation, a
// fake) - see DbContextConfiguration.cs.
builder.AddDbContextConfiguration();

// Reports not-ready until EShop.SellerPortal.MigrationRunner's schema marker says the
// database is migrated - see SchemaMarkerHealthCheck. Skipped in "Fake"
// (SellerPortalWebApplicationFactory registers its own SQLite-backed context with no
// real SQL Server/Service Bus/Storage) and during build-time OpenAPI generation, same
// as the AddServiceDefaults/DbContext guards above.
if (builder.Environment.ShouldUseRealInfrastructure())
{
    builder.Services.AddHealthChecks().AddCheck<SchemaMarkerHealthCheck>("schema-marker");

    builder.AddAzureServiceBusClient(connectionName: KnownNames.ResourceServiceBus);
    builder.AddAzureBlobContainerClient(connectionName: KnownNames.ResourceSellerSubmissionsImage);
    builder.AddRedisClient(connectionName: KnownNames.ResourceCache);
    builder.AddRedisDistributedCache(connectionName: KnownNames.ResourceCache);

    builder.Services.AddHostedService<SubmissionResultConsumer>();
    builder.Services.AddHostedService<SubmissionImageDeletionConsumer>();
    builder.Services.AddHostedService<InventoryResultConsumer>();
}
else if (EnvironmentChecks.IsBuildTimeOpenApiGeneration())
{
    // AddServiceDefaults() is skipped above, but MapDefaultEndpoints() below always
    // maps /health and /alive unconditionally - needs AddHealthChecks() registered
    // regardless of this mock host run, or mapping throws.
    builder.AddDefaultHealthChecks();

    // these two stand-ins only need to exist, never connect.
    // Factory form (not AddSingleton(instance)) so DI disposes this at shutdown -
    // CA2000 otherwise flags the constructed-but-unowned instance.
    builder.Services.AddSingleton(_ => new ServiceBusClient(TestingDefaults.FakeServiceBusConnectionString));
    builder.Services.AddSingleton(new BlobContainerClient(TestingDefaults.FakeStorageConnectionString, TestingDefaults.FakeBlobContainerName));
}

// Cookie/OIDC authentication, the "SellerOnly" authorization policy, and the
// Data Protection/ticket-store persistence backing the auth cookie - see
// AuthConfiguration.cs.
builder.AddAuthConfiguration();

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
