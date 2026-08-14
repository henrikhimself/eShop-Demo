// <copyright file="SellerProvisionerTests.cs" company="Henrik Jensen">
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

using System.Security.Claims;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

public sealed class SellerProvisionerTests : IAsyncLifetime
{
    // Shared-cache SQLite, not the single-connection ":memory:" pattern other test
    // files use: this test needs two independent SqliteConnection instances (matching
    // two real DI-scoped contexts), and one connection object doesn't support
    // concurrent command execution. keepAliveConnection just keeps the shared in-memory
    // data alive for the test's duration.
    private const string ConnectionString = "Data Source=file:seller-provisioner-tests;Mode=Memory;Cache=Shared";

    private SqliteConnection keepAliveConnection = null!;

    public async ValueTask InitializeAsync()
    {
        keepAliveConnection = new SqliteConnection(ConnectionString);
        await keepAliveConnection.OpenAsync(TestContext.Current.CancellationToken);

        await using SellerPortalDbContext db = CreateContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await keepAliveConnection.DisposeAsync();
    }

    // Runs on separate threads with separate connections so both have a real chance to
    // observe "no existing Seller row" before either commits - the race
    // EnsureSellerAsync must catch and recover from.
    [Fact]
    public async Task EnsureSellerAsync_ConcurrentFirstRequestsForSameSubject_BothReturnTheSameSeller()
    {
        ClaimsPrincipal userA = CreatePrincipal("concurrent-subject");
        ClaimsPrincipal userB = CreatePrincipal("concurrent-subject");

        Task<Seller> taskA = Task.Run(async () =>
        {
            await using SellerPortalDbContext dbA = CreateContext();
            return await SellerProvisioner.EnsureSellerAsync(userA, dbA, TestContext.Current.CancellationToken);
        });
        Task<Seller> taskB = Task.Run(async () =>
        {
            await using SellerPortalDbContext dbB = CreateContext();
            return await SellerProvisioner.EnsureSellerAsync(userB, dbB, TestContext.Current.CancellationToken);
        });

        Seller[] sellers = await Task.WhenAll(taskA, taskB);

        Assert.Equal(sellers[0].Id, sellers[1].Id);

        await using SellerPortalDbContext verifyDb = CreateContext();
        int totalSellers = await verifyDb.Sellers.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, totalSellers);
    }

    private static SellerPortalDbContext CreateContext()
    {
        DbContextOptions<SellerPortalDbContext> options = new DbContextOptionsBuilder<SellerPortalDbContext>()
            .UseSqlite(ConnectionString)
            .Options;
        return new SellerPortalDbContext(options);
    }

    private static ClaimsPrincipal CreatePrincipal(string subjectId)
    {
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, subjectId)]);
        return new ClaimsPrincipal(identity);
    }
}
