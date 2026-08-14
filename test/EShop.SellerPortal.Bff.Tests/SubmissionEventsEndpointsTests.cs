// <copyright file="SubmissionEventsEndpointsTests.cs" company="Henrik Jensen">
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
using System.Text.Json;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Data;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Hj.EShop.SellerPortal.Bff.Notifications;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

public sealed class SubmissionEventsEndpointsTests : IAsyncLifetime
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
    public async Task GetEvents_PublishedSummaryForSeller_IsDeliveredAsSseDataLine()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        HttpClient client = CreateAuthenticatedClient("seller-events-test");

        // Any authenticated call is enough to trigger SellerProvisioner's
        // just-in-time Seller row - needed below to publish under the right key.
        (await client.GetAsync(new Uri("/bff/api/submissions", UriKind.Relative), cancellationToken)).EnsureSuccessStatusCode();
        Guid sellerId = await GetSellerIdAsync("seller-events-test", cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        // ResponseHeadersRead: the body never ends on its own, so waiting for the full
        // response (the HttpClient default) would hang forever.
        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/submissions/events", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(stream);

        // The endpoint subscribes before its first write (the "connected" comment
        // that unblocked ResponseHeadersRead above), so this is already true - kept
        // as a safety net against that ordering changing later.
        SubmissionNotificationBroadcaster broadcaster = factory.Services.GetRequiredService<SubmissionNotificationBroadcaster>();
        await WaitUntilSubscribedAsync(broadcaster, sellerId, timeoutCts.Token);

        SubmissionSummary summary = new(
            Guid.CreateVersion7(), "A Movie", SubmissionStatus.Approved, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "SKU-1", null);
        broadcaster.Publish(sellerId, summary);

        string dataLine = await ReadUntilDataLineAsync(reader, timeoutCts.Token);
        string json = dataLine["data: ".Length..];

        // Locks in the actual wire contract the frontend depends on
        // (submission-events.tsx reads `summary.status`, a case-sensitive JS property
        // access) - not just a C#-to-C# round trip, which would still pass even if this
        // endpoint fell back to PascalCase.
        Assert.Contains("\"status\":", json, StringComparison.Ordinal);

        SubmissionSummary? delivered = JsonSerializer.Deserialize<SubmissionSummary>(json, TestJsonOptions.Default);
        Assert.NotNull(delivered);
        Assert.Equal(summary.Id, delivered!.Id);
        Assert.Equal(summary.Status, delivered.Status);
        Assert.Equal(summary.AssignedSku, delivered.AssignedSku);

        Exception? disposeException = Record.Exception(response.Dispose);
        Assert.Null(disposeException);
    }

    [Fact]
    public async Task GetEvents_MultiplePublishedSummaries_AreAllDeliveredInOrder()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        HttpClient client = CreateAuthenticatedClient("seller-events-multi");

        (await client.GetAsync(new Uri("/bff/api/submissions", UriKind.Relative), cancellationToken)).EnsureSuccessStatusCode();
        Guid sellerId = await GetSellerIdAsync("seller-events-multi", cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/submissions/events", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(stream);

        SubmissionNotificationBroadcaster broadcaster = factory.Services.GetRequiredService<SubmissionNotificationBroadcaster>();
        await WaitUntilSubscribedAsync(broadcaster, sellerId, timeoutCts.Token);

        // Guards SubmissionEventsEndpoints' single WaitToReadAsync waiter (created
        // once outside the loop): the endpoint must keep delivering every subsequent
        // publish correctly, not just the very first one, since the waiter is only
        // recreated after actually being consumed.
        SubmissionSummary first = new(
            Guid.CreateVersion7(), "First Movie", SubmissionStatus.Approved, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "SKU-1", null);
        SubmissionSummary second = new(
            Guid.CreateVersion7(), "Second Movie", SubmissionStatus.Approved, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "SKU-2", null);
        broadcaster.Publish(sellerId, first);
        broadcaster.Publish(sellerId, second);

        string firstJson = (await ReadUntilDataLineAsync(reader, timeoutCts.Token))["data: ".Length..];
        string secondJson = (await ReadUntilDataLineAsync(reader, timeoutCts.Token))["data: ".Length..];

        SubmissionSummary? firstDelivered = JsonSerializer.Deserialize<SubmissionSummary>(firstJson, TestJsonOptions.Default);
        SubmissionSummary? secondDelivered = JsonSerializer.Deserialize<SubmissionSummary>(secondJson, TestJsonOptions.Default);
        Assert.Equal(first.Id, firstDelivered?.Id);
        Assert.Equal(second.Id, secondDelivered?.Id);

        response.Dispose();
    }

    [Fact]
    public async Task GetEvents_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/submissions/events", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(string subjectId)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, subjectId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");
        return client;
    }

    private async Task<Guid> GetSellerIdAsync(string subjectId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        SellerPortalDbContext db = scope.ServiceProvider.GetRequiredService<SellerPortalDbContext>();
        Seller seller = await db.Sellers.SingleAsync(s => s.SubjectId == subjectId, cancellationToken);
        return seller.Id;
    }

    private static async Task WaitUntilSubscribedAsync(
        SubmissionNotificationBroadcaster broadcaster, Guid sellerId, CancellationToken cancellationToken)
    {
        while (broadcaster.SubscriberCount(sellerId) == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    private static async Task<string> ReadUntilDataLineAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is { Length: > 0 } && line.StartsWith("data: ", StringComparison.Ordinal))
            {
                return line;
            }
        }
    }
}
