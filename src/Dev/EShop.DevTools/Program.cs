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

using Hj.EShop.Common;
using Hj.EShop.DevTools.Hubs;
using Hj.EShop.DevTools.Services;
using Hj.EShop.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<SellerPendingSubmissionStore>();
builder.Services.AddSingleton<SellerPendingInventoryReportStore>();

// Skipped in the "Fake" test environment (DevToolsWebApplicationFactory registers its
// own recording/fake test doubles instead).
if (builder.Environment.ShouldUseRealInfrastructure())
{
    builder.AddAzureServiceBusClient(connectionName: KnownNames.ResourceServiceBus);
    builder.AddAzureBlobContainerClient(connectionName: KnownNames.ResourceSellerSubmissionsImage);
    builder.Services.AddHostedService<SellerDraftApprovalSimulatorConsumer>();
    builder.Services.AddHostedService<SellerInventoryReportSimulatorConsumer>();
    builder.Services.AddHostedService<SellerSubmissionCancellationConsumer>();
}

builder.Services.AddRazorPages();
builder.Services.AddSignalR();

WebApplication app = builder.Build();

app.MapDefaultEndpoints();
app.UseStaticFiles();
app.MapHub<SellerSubmissionsHub>("/hubs/seller-submissions");
app.MapHub<SellerInventoryHub>("/hubs/seller-inventory");
app.MapRazorPages();

await app.RunAsync();
