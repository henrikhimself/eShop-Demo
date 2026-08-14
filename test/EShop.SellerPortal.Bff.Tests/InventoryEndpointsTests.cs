// <copyright file="InventoryEndpointsTests.cs" company="Henrik Jensen">
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

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Azure.Messaging.ServiceBus;
using Hj.EShop.Messaging;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

public sealed class InventoryEndpointsTests : IAsyncLifetime
{
    private readonly SellerPortalWebApplicationFactory factory = new();

    public async ValueTask InitializeAsync()
    {
        await factory.EnsureDatabaseCreatedAsync();
    }

    public ValueTask DisposeAsync()
    {
        factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetInventory_NoApprovedSubmissions_ReturnsEmptyList()
    {
        HttpClient client = CreateAuthenticatedClient("seller-inventory-empty");

        HttpResponseMessage response = await client.GetAsync(new Uri("/bff/api/inventory", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<InventorySummary>? summaries = await response.Content.ReadFromJsonAsync<List<InventorySummary>>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(summaries);
        Assert.Empty(summaries!);
    }

    [Fact]
    public async Task GetInventory_ApprovedSubmissionWithoutReport_ReturnsSummaryWithNullFields()
    {
        const string subjectId = "seller-inventory-noreport";
        HttpClient client = CreateAuthenticatedClient(subjectId);
        await CreateApprovedSubmissionAsync(subjectId, "SKU-1", "A Movie");

        HttpResponseMessage response = await client.GetAsync(new Uri("/bff/api/inventory", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<InventorySummary>? summaries = await response.Content.ReadFromJsonAsync<List<InventorySummary>>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        InventorySummary summary = Assert.Single(summaries!);
        Assert.Equal("SKU-1", summary.Sku);
        Assert.Equal("A Movie", summary.TitleSnapshot);
        Assert.Null(summary.ReportedQuantity);
        Assert.Null(summary.SyncStatus);
        Assert.Null(summary.LastSyncedAtUtc);
    }

    [Fact]
    public async Task GetInventory_AnotherSellersApprovedSubmission_IsNotIncluded()
    {
        await CreateApprovedSubmissionAsync("seller-inventory-other", "SKU-OTHER", "Someone Else's Movie");
        HttpClient client = CreateAuthenticatedClient("seller-inventory-self");

        HttpResponseMessage response = await client.GetAsync(new Uri("/bff/api/inventory", UriKind.Relative), TestContext.Current.CancellationToken);

        List<InventorySummary>? summaries = await response.Content.ReadFromJsonAsync<List<InventorySummary>>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.Empty(summaries!);
    }

    [Fact]
    public async Task GetInventory_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/bff/api/inventory", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostInventory_ApprovedSku_CreatesSellerInventoryAndPublishesReport()
    {
        const string subjectId = "seller-inventory-report-create";
        HttpClient client = CreateAuthenticatedClient(subjectId);
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        await CreateApprovedSubmissionAsync(subjectId, "SKU-2", "Another Movie");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/bff/api/inventory/SKU-2", UriKind.Relative), new ReportInventoryRequest(10), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        InventorySummary? summary = await response.Content.ReadFromJsonAsync<InventorySummary>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.Equal(10, summary?.ReportedQuantity);
        Assert.Equal(InventorySyncStatus.Pending, summary?.SyncStatus);

        ServiceBusMessage sentMessage = Assert.Single(factory.ServiceBusClient.SentMessages);
        InventoryReportMessage message = JsonSerializer.Deserialize<InventoryReportMessage>(sentMessage.Body.ToString())!;
        Assert.Equal("SKU-2", message.Sku);
        Assert.Equal(10, message.Quantity);
    }

    [Fact]
    public async Task PostInventory_ApprovedSku_BroadcastsPendingSummaryOverSse()
    {
        const string subjectId = "seller-inventory-report-broadcast";
        HttpClient client = CreateAuthenticatedClient(subjectId);
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        await CreateApprovedSubmissionAsync(subjectId, "SKU-BROADCAST", "Broadcast Movie");

        InventoryNotificationBroadcaster broadcaster = factory.Services.GetRequiredService<InventoryNotificationBroadcaster>();
        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();
        Seller seller = await db.Sellers.SingleAsync(s => s.SubjectId == subjectId, TestContext.Current.CancellationToken);
        Channel<InventorySummary> channel = broadcaster.Subscribe(seller.Id);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/bff/api/inventory/SKU-BROADCAST", UriKind.Relative), new ReportInventoryRequest(3), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        InventorySummary broadcastSummary = await channel.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("SKU-BROADCAST", broadcastSummary.Sku);
        Assert.Equal(3, broadcastSummary.ReportedQuantity);
        Assert.Equal(InventorySyncStatus.Pending, broadcastSummary.SyncStatus);

        broadcaster.Unsubscribe(seller.Id, channel);
    }

    [Fact]
    public async Task PostInventory_ExistingReport_UpsertsInsteadOfDuplicating()
    {
        const string subjectId = "seller-inventory-report-update";
        HttpClient client = CreateAuthenticatedClient(subjectId);
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        await CreateApprovedSubmissionAsync(subjectId, "SKU-3", "Yet Another Movie");

        (await client.PostAsJsonAsync(
            new Uri("/bff/api/inventory/SKU-3", UriKind.Relative), new ReportInventoryRequest(5), TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/bff/api/inventory/SKU-3", UriKind.Relative), new ReportInventoryRequest(20), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        InventorySummary? summary = await response.Content.ReadFromJsonAsync<InventorySummary>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.Equal(20, summary?.ReportedQuantity);

        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();
        Assert.Single(db.SellerInventories, i => i.Sku == "SKU-3");
        Assert.Equal(2, factory.ServiceBusClient.SentMessages.Count);
    }

    [Fact]
    public async Task PostInventory_SkuNotApprovedForThisSeller_ReturnsNotFound()
    {
        HttpClient client = CreateAuthenticatedClient("seller-inventory-unowned");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/bff/api/inventory/SKU-404", UriKind.Relative), new ReportInventoryRequest(1), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostInventory_AnotherSellersApprovedSku_ReturnsNotFound()
    {
        await CreateApprovedSubmissionAsync("seller-inventory-owner", "SKU-OWNED", "Owner's Movie");
        HttpClient client = CreateAuthenticatedClient("seller-inventory-intruder");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/bff/api/inventory/SKU-OWNED", UriKind.Relative), new ReportInventoryRequest(1), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostInventory_NegativeQuantity_ReturnsBadRequest()
    {
        const string subjectId = "seller-inventory-negative";
        HttpClient client = CreateAuthenticatedClient(subjectId);
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        await CreateApprovedSubmissionAsync(subjectId, "SKU-4", "Negative Test Movie");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/bff/api/inventory/SKU-4", UriKind.Relative), new ReportInventoryRequest(-1), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostInventory_PublishFailure_ReturnsInternalServerErrorButKeepsCommittedState()
    {
        const string subjectId = "seller-inventory-publishfail";
        HttpClient client = CreateAuthenticatedClient(subjectId);
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);
        await CreateApprovedSubmissionAsync(subjectId, "SKU-5", "Publish Fail Movie");
        factory.ServiceBusClient.ThrowOnSend = true;

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/bff/api/inventory/SKU-5", UriKind.Relative), new ReportInventoryRequest(7), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();
        SellerInventory inventory = await db.SellerInventories.SingleAsync(i => i.Sku == "SKU-5", TestContext.Current.CancellationToken);
        Assert.Equal(7, inventory.ReportedQuantity);
    }

    [Fact]
    public async Task PostInventory_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/bff/api/inventory/SKU-1", UriKind.Relative), new ReportInventoryRequest(1), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(string subjectId)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, subjectId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");
        return client;
    }

    // Bypasses the real submit/approve workflow (covered by SubmissionEndpointsTests/
    // SubmissionResultConsumerTests already) - writes an Approved Submission directly,
    // since this endpoint only cares about the resulting SellerId+AssignedSku pairing.
    private async Task CreateApprovedSubmissionAsync(string subjectId, string sku, string title)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();

        Seller? seller = await db.Sellers.FirstOrDefaultAsync(s => s.SubjectId == subjectId);
        if (seller is null)
        {
            seller = new Seller { Id = Guid.CreateVersion7(), SubjectId = subjectId, CreatedAtUtc = DateTimeOffset.UtcNow };
            db.Sellers.Add(seller);
        }

        db.Submissions.Add(new Submission
        {
            Id = Guid.CreateVersion7(),
            SellerId = seller.Id,
            TitleSnapshot = title,
            KindSnapshot = DraftKind.Movie,
            SubmittedAtUtc = DateTimeOffset.UtcNow,
            RespondedAtUtc = DateTimeOffset.UtcNow,
            Status = SubmissionStatus.Approved,
            AssignedSku = sku,
        });

        await db.SaveChangesAsync();
    }
}
