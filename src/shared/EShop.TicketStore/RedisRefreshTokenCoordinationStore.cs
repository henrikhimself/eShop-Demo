// <copyright file="RedisRefreshTokenCoordinationStore.cs" company="Henrik Jensen">
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
using StackExchange.Redis;

namespace Hj.EShop.TicketStore;

public sealed class RedisRefreshTokenCoordinationStore(IDatabase database) : IRefreshTokenCoordinationStore
{
    public async Task<RefreshTokenExchangeResult?> ReadResultAsync(string key, CancellationToken cancellationToken)
    {
        RedisValue value = await database.StringGetAsync(key).WaitAsync(cancellationToken);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<RefreshTokenExchangeResult>(value.ToString());
    }

    public Task<bool> TryAcquireAsync(string key, string owner, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        return database.LockTakeAsync(key, owner, leaseDuration).WaitAsync(cancellationToken);
    }

    public async Task WriteResultAsync(string key, RefreshTokenExchangeResult result, TimeSpan resultDuration, CancellationToken cancellationToken)
    {
        string value = JsonSerializer.Serialize(result);
        await database.StringSetAsync(key, value, resultDuration).WaitAsync(cancellationToken);
    }

    public async Task ReleaseAsync(string key, string owner, CancellationToken cancellationToken)
    {
        await database.LockReleaseAsync(key, owner).WaitAsync(cancellationToken);
    }
}
