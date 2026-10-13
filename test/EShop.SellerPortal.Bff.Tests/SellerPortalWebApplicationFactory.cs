// <copyright file="SellerPortalWebApplicationFactory.cs" company="Henrik Jensen">
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

using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Hj.EShop.Common;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.Testing.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// Runs the real Program.cs, but swaps every external dependency (SQL Server, Service
// Bus, Blob Storage, Keycloak/OIDC) for something that works with no real
// infrastructure, so these tests exercise the actual auth/authorization pipeline and
// EF Core wiring without needing Docker.
internal sealed class SellerPortalWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");

    public FakeBlobContainerClient BlobContainerClient { get; } = new();

    public RecordingServiceBusClient ServiceBusClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();

        builder.UseEnvironment(KnownNames.FakeEnvironmentName);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["services:keycloak:https:0"] = "https://keycloak.test",
                ["Keycloak:ClientSecret"] = "test-secret",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Program.cs skips Aspire's own AddSqlServerDbContext/AddAzureServiceBusClient/
            // AddAzureBlobContainerClient in the "Fake" environment, so there is
            // nothing to remove here - just register the fakes the draft image and
            // submission endpoints resolve from DI.
            services.AddDbContext<SellerPortalDbContext>(options => options.UseSqlite(connection));
            services.AddSingleton<BlobContainerClient>(BlobContainerClient);
            services.AddSingleton<ServiceBusClient>(ServiceBusClient);

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            connection.Dispose();
        }
    }
}
