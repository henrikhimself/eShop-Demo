// <copyright file="HybridCacheTicketStore.cs" company="Henrik Jensen">
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

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Hybrid;

namespace Hj.EShop.TicketStore;

// Shared by EShop.SellerPortal.Bff and EShop.StoreFront.Web, each registering its own instance with its own key prefix via DI.
public sealed class HybridCacheTicketStore(HybridCache cache, string keyPrefix) : ITicketStore
{
    // Cookie auth's own default ExpireTimeSpan - used only when a ticket has no
    // ExpiresUtc of its own (SlidingExpiration off).
    private static readonly TimeSpan _defaultExpiration = TimeSpan.FromDays(14);

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        string key = Guid.CreateVersion7().ToString();
        await RenewAsync(key, ticket);
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        byte[] bytes = TicketSerializer.Default.Serialize(ticket);
        TimeSpan expiration = ticket.Properties.ExpiresUtc is { } expiresUtc
            ? expiresUtc - DateTimeOffset.UtcNow
            : _defaultExpiration;
        HybridCacheEntryOptions options = new() { Expiration = expiration };

        await cache.SetAsync(CacheKey(key), bytes, options);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        // HybridCache has no bare "get" - GetOrCreateAsync with a no-op factory is the
        // documented way to read one. A missing key round-trips as an empty array, not
        // null, and a real ticket is never zero bytes, so empty means "not found".
        byte[] bytes = await cache.GetOrCreateAsync(
            CacheKey(key), static _ => ValueTask.FromResult(Array.Empty<byte>()));

        return bytes.Length == 0 ? null : TicketSerializer.Default.Deserialize(bytes);
    }

    public async Task RemoveAsync(string key)
    {
        await cache.RemoveAsync(CacheKey(key));
    }

    private string CacheKey(string key)
    {
        return keyPrefix + key;
    }
}
