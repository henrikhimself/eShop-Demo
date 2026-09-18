// <copyright file="HybridCacheTicketStoreTests.cs" company="Henrik Jensen">
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
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hj.EShop.TicketStore.Tests;

// L1-only (no IDistributedCache registered) is enough to exercise the store's own
// serialize/store/retrieve logic - no live Valkey needed.
public sealed class HybridCacheTicketStoreTests
{
    [Fact]
    public async Task StoreAsync_ThenRetrieveAsync_RoundTripsTheTicketsClaims()
    {
        HybridCacheTicketStore store = CreateStore();
        AuthenticationTicket ticket = CreateTicket("a-seller");

        string key = await store.StoreAsync(ticket);
        AuthenticationTicket? retrieved = await store.RetrieveAsync(key);

        Assert.NotNull(retrieved);
        Assert.Equal("a-seller", retrieved!.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task RetrieveAsync_UnknownKey_ReturnsNull()
    {
        HybridCacheTicketStore store = CreateStore();

        AuthenticationTicket? retrieved = await store.RetrieveAsync(Guid.CreateVersion7().ToString());

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task RenewAsync_ReplacesTheStoredTicket()
    {
        HybridCacheTicketStore store = CreateStore();
        string key = await store.StoreAsync(CreateTicket("first-seller"));

        await store.RenewAsync(key, CreateTicket("second-seller"));
        AuthenticationTicket? retrieved = await store.RetrieveAsync(key);

        Assert.Equal("second-seller", retrieved!.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task RemoveAsync_ThenRetrieveAsync_ReturnsNull()
    {
        HybridCacheTicketStore store = CreateStore();
        string key = await store.StoreAsync(CreateTicket("a-seller"));

        await store.RemoveAsync(key);
        AuthenticationTicket? retrieved = await store.RetrieveAsync(key);

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task StoreAsync_DifferentKeyPrefixes_DoNotCollide()
    {
        ServiceCollection services = new();
        services.AddHybridCache();
        HybridCache cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();
        HybridCacheTicketStore sellerPortalStore = new(cache, "seller-portal-ticket:");
        HybridCacheTicketStore storefrontStore = new(cache, "storefront-ticket:");

        string key = await sellerPortalStore.StoreAsync(CreateTicket("a-seller"));

        Assert.Null(await storefrontStore.RetrieveAsync(key));
        Assert.NotNull(await sellerPortalStore.RetrieveAsync(key));
    }

    private static HybridCacheTicketStore CreateStore()
    {
        ServiceCollection services = new();
        services.AddHybridCache();
        HybridCache cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();

        return new HybridCacheTicketStore(cache, "seller-portal-ticket:");
    }

    private static AuthenticationTicket CreateTicket(string subjectId)
    {
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, subjectId)], "Cookies");
        ClaimsPrincipal principal = new(identity);
        AuthenticationProperties properties = new() { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) };

        return new AuthenticationTicket(principal, properties, "Cookies");
    }
}
