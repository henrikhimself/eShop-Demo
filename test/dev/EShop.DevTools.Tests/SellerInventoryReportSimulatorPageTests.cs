// <copyright file="SellerInventoryReportSimulatorPageTests.cs" company="Henrik Jensen">
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

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Messaging.ServiceBus;
using Hj.EShop.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Hj.EShop.DevTools.Tests;

// Mirrors SellerDraftApprovalSimulatorPageTests - see that type for the reasoning
// behind exercising the real Razor Pages antiforgery pipeline with no custom bypass.
public sealed partial class SellerInventoryReportSimulatorPageTests
{
    private static readonly Uri PageUri = new("/Tools/SellerInventoryReportSimulator", UriKind.Relative);

    [Fact]
    public async Task GetToolPage_ListsWhateverIsInThePendingInventoryReportStore()
    {
        using DevToolsWebApplicationFactory factory = new();
        InventoryReportMessage message = CreateMessage();
        factory.InventoryStore.Add(message);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(PageUri, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(message.Sku, html, StringComparison.Ordinal);
        Assert.Contains(message.Quantity.ToString(CultureInfo.InvariantCulture), html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostConfirm_PublishesConfirmedResultAndRemovesFromStore()
    {
        using DevToolsWebApplicationFactory factory = new();
        InventoryReportMessage message = CreateMessage();
        factory.InventoryStore.Add(message);
        HttpClient client = CreateClient(factory);
        string token = await GetAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["sellerId"] = message.SellerId.ToString(),
            ["sku"] = message.Sku,
            ["__RequestVerificationToken"] = token,
        });
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/Tools/SellerInventoryReportSimulator?handler=Confirm", UriKind.Relative), form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        ServiceBusMessage sentMessage = Assert.Single(factory.ServiceBusClient.SentMessages);
        InventoryResultMessage result = JsonSerializer.Deserialize<InventoryResultMessage>(sentMessage.Body.ToString())!;
        Assert.Equal(message.SellerId, result.SellerId);
        Assert.Equal(message.Sku, result.Sku);
        Assert.True(result.Confirmed);
        Assert.Null(result.Error);
        Assert.False(factory.InventoryStore.TryGet(message.SellerId, message.Sku, out _));
    }

    [Fact]
    public async Task PostFail_PublishesFailedResultWithErrorAndRemovesFromStore()
    {
        using DevToolsWebApplicationFactory factory = new();
        InventoryReportMessage message = CreateMessage();
        factory.InventoryStore.Add(message);
        HttpClient client = CreateClient(factory);
        string token = await GetAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["sellerId"] = message.SellerId.ToString(),
            ["sku"] = message.Sku,
            ["error"] = "Warehouse sync failed.",
            ["__RequestVerificationToken"] = token,
        });
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/Tools/SellerInventoryReportSimulator?handler=Fail", UriKind.Relative), form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        ServiceBusMessage sentMessage = Assert.Single(factory.ServiceBusClient.SentMessages);
        InventoryResultMessage result = JsonSerializer.Deserialize<InventoryResultMessage>(sentMessage.Body.ToString())!;
        Assert.Equal(message.SellerId, result.SellerId);
        Assert.Equal(message.Sku, result.Sku);
        Assert.False(result.Confirmed);
        Assert.Equal("Warehouse sync failed.", result.Error);
        Assert.False(factory.InventoryStore.TryGet(message.SellerId, message.Sku, out _));
    }

    internal static HttpClient CreateClient(DevToolsWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
    }

    internal static InventoryReportMessage CreateMessage()
    {
        return new InventoryReportMessage(Guid.CreateVersion7(), "SKU-1", 10);
    }

    internal static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.GetAsync(PageUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        string html = await response.Content.ReadAsStringAsync(cancellationToken);
        Match match = AntiforgeryTokenRegex().Match(html);

        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException("The tool page did not render an antiforgery token.");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
