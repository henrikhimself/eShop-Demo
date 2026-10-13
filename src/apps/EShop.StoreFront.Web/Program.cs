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

// Storefront uses classic hosting.
// Host.CreateDefaultBuilder reads DOTNET_-prefixed bootstrap variables; launchSettings sets both.
static IHostBuilder CreateHostBuilder(string[] args)
{
    return Host.CreateDefaultBuilder(args)
        .ConfigureCmsDefaults()
        .ConfigureLogging(logging => logging.ConfigureOpenTelemetryLogging())
        .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
}

// The preflight runs before the real host, so it recreates the host configuration sources.
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
