// <copyright file="SellerPortalDbContextTests.cs" company="Henrik Jensen">
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

using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// Uses the SQLite relational provider, not EF Core's InMemory provider: InMemory
// silently ignores unique-index violations, which is exactly what these tests check.
public sealed class SellerPortalDbContextTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private SellerPortalDbContext db = null!;

    public async ValueTask InitializeAsync()
    {
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<SellerPortalDbContext> options = new DbContextOptionsBuilder<SellerPortalDbContext>()
            .UseSqlite(connection)
            .Options;

        db = new SellerPortalDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task MovieDraft_WithFormatVariants_RoundTrips()
    {
        Seller seller = new()
        {
            Id = Guid.CreateVersion7(),
            SubjectId = "seller-1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        MovieDraft draft = new()
        {
            Id = Guid.CreateVersion7(),
            SellerId = seller.Id,
            Title = "A Movie",
            Genre = "Drama",
            Description = "A description.",
            YearOfRelease = 2026,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        draft.FormatVariants.Add(new DraftFormatVariant
        {
            Id = Guid.CreateVersion7(),
            DraftId = draft.Id,
            Format = MovieFormat.Dvd,
            Price = 9.99m,
        });

        db.Sellers.Add(seller);
        db.Drafts.Add(draft);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.ChangeTracker.Clear();

        MovieDraft reloaded = await db.Drafts.OfType<MovieDraft>()
            .Include(d => d.FormatVariants)
            .SingleAsync(d => d.Id == draft.Id, TestContext.Current.CancellationToken);

        Assert.Equal("A Movie", reloaded.Title);
        Assert.Single(reloaded.FormatVariants);
        Assert.Equal(MovieFormat.Dvd, reloaded.FormatVariants[0].Format);
    }

    [Fact]
    public async Task Seller_DuplicateSubjectId_ViolatesUniqueIndex()
    {
        db.Sellers.Add(new Seller
        {
            Id = Guid.CreateVersion7(),
            SubjectId = "duplicate-subject",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Sellers.Add(new Seller
        {
            Id = Guid.CreateVersion7(),
            SubjectId = "duplicate-subject",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SellerInventory_DuplicateSellerAndSku_ViolatesUniqueIndex()
    {
        Seller seller = new()
        {
            Id = Guid.CreateVersion7(),
            SubjectId = "inventory-seller",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        db.Sellers.Add(seller);
        Guid sellerId = seller.Id;
        db.SellerInventories.Add(new SellerInventory
        {
            Id = Guid.CreateVersion7(),
            SellerId = sellerId,
            Sku = "SKU-1",
            ReportedQuantity = 5,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.SellerInventories.Add(new SellerInventory
        {
            Id = Guid.CreateVersion7(),
            SellerId = sellerId,
            Sku = "SKU-1",
            ReportedQuantity = 10,
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
