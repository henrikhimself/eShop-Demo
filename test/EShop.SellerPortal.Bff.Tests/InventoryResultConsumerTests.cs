// <copyright file="InventoryResultConsumerTests.cs" company="Henrik Jensen">
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

using System.Text.Json;
using System.Threading.Channels;
using Hj.EShop.Messaging;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Messaging;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// Calls HandleMessageAsync directly (internal, not protected - see
// ServiceBusQueueConsumer): ProcessMessageEventArgs has no testable constructor
// without a live broker, but the dead-letter-vs-complete decision is a pure function
// of the return value this exercises.
public sealed class InventoryResultConsumerTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly InventoryNotificationBroadcaster broadcaster = new();
    private IServiceScopeFactory scopeFactory = null!;
    private SellerPortalDbContext db = null!;

    public async ValueTask InitializeAsync()
    {
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ServiceProvider provider = new ServiceCollection()
            .AddDbContext<SellerPortalDbContext>(options => options.UseSqlite(connection))
            .BuildServiceProvider();
        scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        db = provider.GetRequiredService<SellerPortalDbContext>();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task HandleMessageAsync_KnownSellerAndSku_UpdatesInventoryAndReturnsHandled()
    {
        Seller seller = new() { Id = Guid.CreateVersion7(), SubjectId = "seller-1", CreatedAtUtc = DateTimeOffset.UtcNow };
        db.Sellers.Add(seller);
        db.SellerInventories.Add(new SellerInventory { Id = Guid.CreateVersion7(), SellerId = seller.Id, Sku = "SKU-1", ReportedQuantity = 5 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        InventoryResultConsumer consumer = new(client: null!, scopeFactory, broadcaster, NullLogger<InventoryResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new { SellerId = seller.Id, Sku = "SKU-1", Confirmed = true, Error = (string?)null }));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        SellerInventory updated = await db.SellerInventories.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(InventorySyncStatus.Confirmed, updated.SyncStatus);
    }

    [Fact]
    public async Task HandleMessageAsync_UnknownSellerAndSku_ReturnsUnknownRecordWithoutThrowing()
    {
        InventoryResultConsumer consumer = new(client: null!, scopeFactory, broadcaster, NullLogger<InventoryResultConsumer>.Instance);
        BinaryData body = new(JsonSerializer.Serialize(new { SellerId = Guid.CreateVersion7(), Sku = "SKU-404", Confirmed = true, Error = (string?)null }));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.UnknownRecord, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleMessageAsync_KnownSellerAndSku_BroadcastsUpdatedSummary(bool confirmed)
    {
        Seller seller = new() { Id = Guid.CreateVersion7(), SubjectId = "seller-1", CreatedAtUtc = DateTimeOffset.UtcNow };
        Submission submission = new()
        {
            Id = Guid.CreateVersion7(),
            SellerId = seller.Id,
            DraftId = null,
            TitleSnapshot = "A Movie",
            KindSnapshot = DraftKind.Movie,
            SubmittedAtUtc = DateTimeOffset.UtcNow,
            RespondedAtUtc = DateTimeOffset.UtcNow,
            Status = SubmissionStatus.Approved,
            AssignedSku = "SKU-1",
        };
        db.Sellers.Add(seller);
        db.Submissions.Add(submission);
        db.SellerInventories.Add(new SellerInventory { Id = Guid.CreateVersion7(), SellerId = seller.Id, Sku = "SKU-1", ReportedQuantity = 5 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        Channel<InventorySummary> channel = broadcaster.Subscribe(seller.Id);

        InventoryResultConsumer consumer = new(client: null!, scopeFactory, broadcaster, NullLogger<InventoryResultConsumer>.Instance);
        string? error = confirmed ? null : "Sync failed.";
        BinaryData body = new(JsonSerializer.Serialize(new { SellerId = seller.Id, Sku = "SKU-1", Confirmed = confirmed, Error = error }));

        MessageHandlingResult result = await consumer.HandleMessageAsync(body, TestContext.Current.CancellationToken);

        Assert.Equal(MessageHandlingResult.Handled, result);
        Assert.True(channel.Reader.TryRead(out InventorySummary? publishedSummary));
        Assert.Equal("SKU-1", publishedSummary!.Sku);
        Assert.Equal("A Movie", publishedSummary.TitleSnapshot);
        Assert.Equal(confirmed ? InventorySyncStatus.Confirmed : InventorySyncStatus.Failed, publishedSummary.SyncStatus);
        Assert.Equal(error, publishedSummary.LastError);
    }
}

