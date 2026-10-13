// <copyright file="RefreshTokenExchangeCoordinator.cs" company="Henrik Jensen">
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

using System.Security.Cryptography;
using System.Text;

namespace Hj.EShop.TicketStore;

// Coordinates one use of a rotating refresh token across all application instances.
// The Redis result is intentionally short lived: it only lets callers that already read
// the same ticket use the winner's replacement tokens instead of spending the old token.
public sealed class RefreshTokenExchangeCoordinator(
    IRefreshTokenCoordinationStore store,
    string keyPrefix,
    TimeProvider timeProvider) : IRefreshTokenCoordinator
{
    private static readonly TimeSpan _leaseDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _resultDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _waitDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(50);

    public async Task<RefreshOutcome> RefreshAsync(
        string refreshToken,
        Func<CancellationToken, Task<RefreshOutcome>> refreshAsync,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        ArgumentNullException.ThrowIfNull(refreshAsync);

        string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        string resultKey = keyPrefix + "result:" + tokenHash;
        string lockKey = keyPrefix + "lock:" + tokenHash;
        DateTimeOffset deadline = timeProvider.GetUtcNow().Add(_waitDuration);

        while (true)
        {
            RefreshTokenExchangeResult? existingResult = await ReadResultAsync(resultKey, cancellationToken);
            if (existingResult is not null)
            {
                return existingResult.ToOutcome();
            }

            string owner = Guid.CreateVersion7().ToString();
            bool ownsLock = await TryAcquireAsync(lockKey, owner, cancellationToken);
            if (ownsLock)
            {
                try
                {
                    existingResult = await ReadResultAsync(resultKey, cancellationToken);
                    if (existingResult is not null)
                    {
                        return existingResult.ToOutcome();
                    }

                    using var leaseCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    leaseCancellation.CancelAfter(_leaseDuration);

                    RefreshOutcome outcome;
                    try
                    {
                        outcome = await refreshAsync(leaseCancellation.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new RefreshTokenCoordinationException("The refresh-token exchange exceeded its coordination lease.");
                    }

                    if (leaseCancellation.IsCancellationRequested)
                    {
                        throw new RefreshTokenCoordinationException("The refresh-token exchange exceeded its coordination lease.");
                    }

                    await WriteResultAsync(resultKey, RefreshTokenExchangeResult.From(outcome), cancellationToken);
                    return outcome;
                }
                finally
                {
                    await ReleaseAsync(lockKey, owner);
                }
            }

            if (timeProvider.GetUtcNow() >= deadline)
            {
                throw new RefreshTokenCoordinationException("Timed out waiting for the concurrent refresh-token exchange.");
            }

            await Task.Delay(_pollInterval, timeProvider, cancellationToken);
        }
    }

    private async Task<RefreshTokenExchangeResult?> ReadResultAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await store.ReadResultAsync(key, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new RefreshTokenCoordinationException("Could not read the refresh-token coordination result.", exception);
        }
    }

    private async Task<bool> TryAcquireAsync(string key, string owner, CancellationToken cancellationToken)
    {
        try
        {
            return await store.TryAcquireAsync(key, owner, _leaseDuration, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new RefreshTokenCoordinationException("Could not acquire the refresh-token coordination lease.", exception);
        }
    }

    private async Task WriteResultAsync(string key, RefreshTokenExchangeResult result, CancellationToken cancellationToken)
    {
        try
        {
            await store.WriteResultAsync(key, result, _resultDuration, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new RefreshTokenCoordinationException("Could not write the refresh-token coordination result.", exception);
        }
    }

    private async Task ReleaseAsync(string key, string owner)
    {
        try
        {
            await store.ReleaseAsync(key, owner, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new RefreshTokenCoordinationException("Could not release the refresh-token coordination lease.", exception);
        }
    }
}

public sealed class RefreshTokenCoordinationException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface IRefreshTokenCoordinationStore
{
    Task<RefreshTokenExchangeResult?> ReadResultAsync(string key, CancellationToken cancellationToken);

    Task<bool> TryAcquireAsync(string key, string owner, TimeSpan leaseDuration, CancellationToken cancellationToken);

    Task WriteResultAsync(string key, RefreshTokenExchangeResult result, TimeSpan resultDuration, CancellationToken cancellationToken);

    Task ReleaseAsync(string key, string owner, CancellationToken cancellationToken);
}

public sealed record RefreshTokenExchangeResult(RefreshOutcomeKind Kind, string? IdToken, string? RefreshToken)
{
    public static RefreshTokenExchangeResult From(RefreshOutcome outcome)
    {
        return new(outcome.Kind, outcome.Tokens?.IdToken, outcome.Tokens?.RefreshToken);
    }

    public RefreshOutcome ToOutcome()
    {
        return Kind switch
        {
            RefreshOutcomeKind.NothingToValidate => RefreshOutcome.NothingToValidate,
            RefreshOutcomeKind.StillValid => RefreshOutcome.StillValid,
            RefreshOutcomeKind.Refreshed when IdToken is not null => RefreshOutcome.Refreshed(new RefreshedTokens(IdToken, RefreshToken)),
            RefreshOutcomeKind.Refreshed => RefreshOutcome.Rejected,
            RefreshOutcomeKind.Rejected => RefreshOutcome.Rejected,
            var unknownKind => throw new ArgumentOutOfRangeException(nameof(Kind), unknownKind, null),
        };
    }
}
