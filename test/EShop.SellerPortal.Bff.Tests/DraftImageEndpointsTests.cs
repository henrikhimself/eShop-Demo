// <copyright file="DraftImageEndpointsTests.cs" company="Henrik Jensen">
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
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Hj.EShop.SellerPortal.Bff.Contracts;
using Hj.EShop.SellerPortal.Bff.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Hj.EShop.SellerPortal.Bff.Tests;

public sealed class DraftImageEndpointsTests : IAsyncLifetime
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
    public async Task PostImage_ValidJpeg_StoresBlobAndStreamsItBackFromGet()
    {
        (HttpClient client, Guid draftId) = await CreateDraftAsync("seller-image-upload");
        byte[] imageBytes = [1, 2, 3, 4, 5];

        HttpResponseMessage postResponse = await PostImageAsync(client, draftId, imageBytes, "image/jpeg");

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        DraftImageDto? image = await postResponse.Content.ReadFromJsonAsync<DraftImageDto>(TestJsonOptions.Default, TestContext.Current.CancellationToken);
        Assert.NotNull(image);

        HttpResponseMessage getResponse = await client.GetAsync(new Uri(image!.Url, UriKind.Relative), TestContext.Current.CancellationToken);
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal(imageBytes, await getResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PostImage_Replacing_DeletesThePreviousBlob()
    {
        (HttpClient client, Guid draftId) = await CreateDraftAsync("seller-image-replace");
        HttpResponseMessage firstUpload = await PostImageAsync(client, draftId, [1], "image/png");
        DraftImageDto firstImage = (await firstUpload.Content.ReadFromJsonAsync<DraftImageDto>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;

        HttpResponseMessage secondUpload = await PostImageAsync(client, draftId, [2, 2], "image/png");

        Assert.Equal(HttpStatusCode.OK, secondUpload.StatusCode);
        DraftImageDto secondImage = (await secondUpload.Content.ReadFromJsonAsync<DraftImageDto>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;
        Assert.NotEqual(firstImage.Id, secondImage.Id);

        HttpResponseMessage getFirst = await client.GetAsync(new Uri(firstImage.Url, UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, getFirst.StatusCode);
    }

    [Fact]
    public async Task PostImage_UnsupportedContentType_ReturnsBadRequest()
    {
        (HttpClient client, Guid draftId) = await CreateDraftAsync("seller-image-badtype");

        HttpResponseMessage response = await PostImageAsync(client, draftId, [1, 2, 3], "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostImage_TooLarge_ReturnsBadRequest()
    {
        (HttpClient client, Guid draftId) = await CreateDraftAsync("seller-image-toolarge");
        byte[] tooLarge = new byte[5 * 1024 * 1024 + 1];

        HttpResponseMessage response = await PostImageAsync(client, draftId, tooLarge, "image/jpeg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostImage_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        (HttpClient authenticatedClient, Guid draftId) = await CreateDraftAsync("seller-image-noantiforgery");
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, "seller-image-noantiforgery");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");

        HttpResponseMessage response = await PostImageAsync(client, draftId, [1], "image/jpeg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        authenticatedClient.Dispose();
    }

    [Fact]
    public async Task DeleteImage_InDraftStatus_RemovesRowAndQueuesBlobDeletion()
    {
        (HttpClient client, Guid draftId) = await CreateDraftAsync("seller-image-delete");
        HttpResponseMessage upload = await PostImageAsync(client, draftId, [1, 2], "image/webp");
        DraftImageDto image = (await upload.Content.ReadFromJsonAsync<DraftImageDto>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;

        HttpResponseMessage deleteResponse = await client.DeleteAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/images/{image.Id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // The row is gone immediately (durable intent), but the actual blob delete is
        // deferred to SubmissionImageDeletionConsumer - it's still there right after
        // the response, with a deletion message queued for it.
        Assert.NotEmpty(factory.BlobContainerClient.Blobs);
        ServiceBusMessage sentMessage = Assert.Single(factory.ServiceBusClient.SentMessages);
        BlobDeletionMessage deletionMessage = JsonSerializer.Deserialize<BlobDeletionMessage>(sentMessage.Body.ToString())!;
        Assert.Equal(draftId, deletionMessage.DraftId);
        Assert.Equal(image.Id, deletionMessage.DraftImageId);
    }

    private async Task<(HttpClient Client, Guid DraftId)> CreateDraftAsync(string subjectId)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeaderName, subjectId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeaderName, "Seller");
        await AntiforgeryTestSupport.IssueAntiforgeryTokenAsync(client, TestContext.Current.CancellationToken);

        HttpResponseMessage response = await client.PostAsync(
            new Uri("/bff/api/drafts/movies", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        MovieDraftDetail detail = (await response.Content.ReadFromJsonAsync<MovieDraftDetail>(TestJsonOptions.Default, TestContext.Current.CancellationToken))!;

        return (client, detail.Id);
    }

    private static async Task<HttpResponseMessage> PostImageAsync(HttpClient client, Guid draftId, byte[] content, string contentType)
    {
        using MultipartFormDataContent form = new();
        using ByteArrayContent fileContent = new(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", "cover");

        return await client.PostAsync(
            new Uri($"/bff/api/drafts/movies/{draftId}/images", UriKind.Relative), form, TestContext.Current.CancellationToken);
    }
}
