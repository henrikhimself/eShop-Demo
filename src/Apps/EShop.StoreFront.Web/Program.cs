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
using Hj.EShop.StoreFront.Web.Initialization;

// Classic hosting model, not this repo's usual minimal-hosting pattern - see
// doc/CHRONICLE.md/doc/MEMORY.md for why (AddCms()/AddCommerce() don't work under
// WebApplication.CreateBuilder).
//
// Host.CreateDefaultBuilder's own bootstrap config only reads DOTNET_-prefixed
// environment variables, not ASPNETCORE_ENVIRONMENT directly; launchSettings.json sets
// both for exactly this reason, so no code-level workaround is needed here.
static IHostBuilder CreateHostBuilder(string[] args)
{
    return Host.CreateDefaultBuilder(args)
        .ConfigureCmsDefaults()
        .ConfigureLogging(logging => logging.ConfigureOpenTelemetryLogging())
        .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
}

// Same configuration sources Host.CreateDefaultBuilder itself would set up
// because StorefrontMigrationPreflight must run before the real host.
string? environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
IConfigurationRoot preflightConfiguration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

using ILoggerFactory preflightLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
ILogger preflightLogger = preflightLoggerFactory.CreateLogger("StorefrontMigrationPreflight");

await MigrationPreflight.WaitForBothMigrationsAsync(preflightConfiguration, preflightLogger, CancellationToken.None);

await CreateHostBuilder(args).Build().RunAsync();
