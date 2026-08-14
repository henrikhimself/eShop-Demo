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

using Hj.EShop.ServiceDefaults;
using Hj.EShop.StoreFront.Web;

// Classic hosting model (Host.CreateDefaultBuilder + ConfigureWebHostDefaults +
// UseStartup<Startup>), not the minimal-hosting pattern this repo otherwise uses
// (compare EShop.SellerPortal.Bff/Program.cs). Verified empirically: EPiServer CMS
// 13/Commerce Connect 15's own reflective options-configuration helper (invoked from
// AddCms()/AddCommerce()) fails to resolve IConfiguration when hosted under
// WebApplication.CreateBuilder - see doc/CHRONICLE.md for the investigation.
// ConfigureCmsDefaults() is required here too - it wires CMS-specific host
// configuration this pattern needs before ConfigureWebHostDefaults runs.
static IHostBuilder CreateHostBuilder(string[] args)
{
    return Host.CreateDefaultBuilder(args)
        .ConfigureCmsDefaults()
        .ConfigureLogging(logging => logging.ConfigureOpenTelemetryLogging())
        .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
}

// Same configuration sources Host.CreateDefaultBuilder itself would set up
// (environment variables, appsettings.json/.{Environment}.json) - built standalone here
// because StorefrontMigrationPreflight must run before the real host (and its
// EPiServer.Data.DatabaseSchemaHost hosted service) exists at all - see
// StorefrontMigrationPreflight.cs.
string? environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
IConfigurationRoot preflightConfiguration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

using ILoggerFactory preflightLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
ILogger preflightLogger = preflightLoggerFactory.CreateLogger("StorefrontMigrationPreflight");

await StorefrontMigrationPreflight.WaitForBothMigrationsAsync(preflightConfiguration, preflightLogger, CancellationToken.None);

await CreateHostBuilder(args).Build().RunAsync();
