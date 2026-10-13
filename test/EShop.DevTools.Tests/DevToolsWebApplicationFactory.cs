// <copyright file="DevToolsWebApplicationFactory.cs" company="Henrik Jensen">
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
using Hj.EShop.DevTools.Services;
using Hj.EShop.Testing.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Hj.EShop.DevTools.Tests;

// Runs the real Program.cs, but swaps Service Bus for a recording test double and skips
// the hosted queue consumer - see Program.cs's "Fake" environment check - so these
// tests exercise the actual Razor Pages handlers with no real broker. Program.cs
// registers SellerPendingSubmissionStore unconditionally (nothing environment-dependent
// about it), so tests read it back via Services rather than swapping it out.
internal sealed class DevToolsWebApplicationFactory : WebApplicationFactory<Program>
{
    public RecordingServiceBusClient ServiceBusClient { get; } = new();

    public FakeBlobContainerClient BlobContainerClient { get; } = new();

    public SellerPendingSubmissionStore Store => Services.GetRequiredService<SellerPendingSubmissionStore>();

    public SellerPendingInventoryReportStore InventoryStore => Services.GetRequiredService<SellerPendingInventoryReportStore>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(KnownNames.FakeEnvironmentName);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ServiceBusClient>(ServiceBusClient);
            services.AddSingleton<BlobContainerClient>(BlobContainerClient);
        });
    }
}
