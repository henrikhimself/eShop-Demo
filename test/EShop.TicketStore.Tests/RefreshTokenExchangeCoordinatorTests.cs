// <copyright file="RefreshTokenExchangeCoordinatorTests.cs" company="Henrik Jensen">
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

using Xunit;

namespace Hj.EShop.TicketStore.Tests;

public sealed class RefreshTokenExchangeCoordinatorTests
{
    [Fact]
    public async Task RefreshAsync_ConcurrentCalls_UsesOneExchangeAndSharesRotatedTokens()
    {
        InMemoryCoordinationStore store = new();
        RefreshTokenExchangeCoordinator coordinator = new(store, "seller-portal-refresh:", TimeProvider.System);
        TaskCompletionSource exchangeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource completeExchange = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int exchanges = 0;

        Task<RefreshOutcome> first = coordinator.RefreshAsync(
            "old-refresh-token",
            async cancellationToken =>
            {
                Interlocked.Increment(ref exchanges);
                exchangeStarted.SetResult();
                await completeExchange.Task.WaitAsync(cancellationToken);
                return RefreshOutcome.Refreshed(new RefreshedTokens("fresh-id-token", "fresh-refresh-token"));
            },
            TestContext.Current.CancellationToken);

        await exchangeStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        Task<RefreshOutcome> second = coordinator.RefreshAsync(
            "old-refresh-token",
            _ => throw new Xunit.Sdk.XunitException("The waiter must not call the token endpoint."),
            TestContext.Current.CancellationToken);

        completeExchange.SetResult();
        RefreshOutcome[] outcomes = await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, exchanges);
        Assert.All(outcomes, outcome =>
        {
            Assert.Equal(RefreshOutcomeKind.Refreshed, outcome.Kind);
            Assert.Equal("fresh-id-token", outcome.Tokens?.IdToken);
            Assert.Equal("fresh-refresh-token", outcome.Tokens?.RefreshToken);
        });
        Assert.StartsWith("seller-portal-refresh:result:", store.LastResultKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshAsync_StoreFailure_ThrowsCoordinationException()
    {
        RefreshTokenExchangeCoordinator coordinator = new(new FailingCoordinationStore(), "storefront-refresh:", TimeProvider.System);

        RefreshTokenCoordinationException exception = await Assert.ThrowsAsync<RefreshTokenCoordinationException>(
            () => coordinator.RefreshAsync(
                "old-refresh-token",
                _ => Task.FromResult(RefreshOutcome.Rejected),
                TestContext.Current.CancellationToken));

        Assert.Contains("read", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InMemoryCoordinationStore : IRefreshTokenCoordinationStore
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<string, RefreshTokenExchangeResult> _results = [];
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string? _owner;

        public string LastResultKey { get; private set; } = string.Empty;

        public Task<RefreshTokenExchangeResult?> ReadResultAsync(string key, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                return Task.FromResult(_results.GetValueOrDefault(key));
            }
        }

        public async Task<bool> TryAcquireAsync(string key, string owner, TimeSpan leaseDuration, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_owner is null)
                {
                    _owner = owner;
                    return true;
                }
            }

            await _released.Task.WaitAsync(cancellationToken);
            return false;
        }

        public Task WriteResultAsync(string key, RefreshTokenExchangeResult result, TimeSpan resultDuration, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                LastResultKey = key;
                _results[key] = result;
            }

            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string key, string owner, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_owner == owner)
                {
                    _owner = null;
                    _released.TrySetResult();
                }
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FailingCoordinationStore : IRefreshTokenCoordinationStore
    {
        public Task<RefreshTokenExchangeResult?> ReadResultAsync(string key, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Redis is unavailable.");
        }

        public Task<bool> TryAcquireAsync(string key, string owner, TimeSpan leaseDuration, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task WriteResultAsync(string key, RefreshTokenExchangeResult result, TimeSpan resultDuration, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ReleaseAsync(string key, string owner, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
