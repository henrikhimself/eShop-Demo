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

using Hj.EShop.StoreFront.MigrationRunner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// One-shot, one component per invocation (see StorefrontComponent) - acquire the
// migration lock, apply Optimizely's own shipped SQL scripts, write a schema marker,
// release the lock, exit (see StorefrontSchemaMigrator.cs for that flow). No HTTP
// endpoint, same reasoning as EShop.SellerPortal.MigrationRunner (see doc/CHRONICLE.md).
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
using IHost host = builder.Build();
ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MigrationRunner");

try
{
    StorefrontComponent component = StorefrontComponentParser.Parse(args);
    var options = StorefrontMigrationOptions.FromConfiguration(component, builder.Configuration);
    return await StorefrontSchemaMigrator.RunAsync(options, logger, CancellationToken.None);
}
catch (ArgumentException ex)
{
    logger.LogError(ex, "Usage: EShop.StoreFront.MigrationRunner <cms|commerce>");
    return 1;
}
