// <copyright file="AnthropicMessagesClient.cs" company="Henrik Jensen">
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

using Hj.EShop.StoreFront.EpicwebAI.Bridge.Configuration;
using Hj.EShop.StoreFront.EpicwebAI.Bridge.Models;
using Microsoft.Extensions.Options;

namespace Hj.EShop.StoreFront.EpicwebAI.Bridge.Services;

internal sealed class AnthropicMessagesClient(
    HttpClient httpClient,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicMessagesClient> logger)
{
    private const string ApiVersion = "2023-06-01";
    private readonly AnthropicOptions _options = options.Value;

    public async Task<AnthropicMessageResponse> CreateMessageAsync(AnthropicMessageRequest request, CancellationToken cancellationToken)
    {
        using HttpRequestMessage httpRequest = new(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Add("x-api-key", _options.ApiKey);
        httpRequest.Headers.Add("anthropic-version", ApiVersion);

        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        string? requestId = response.Headers.TryGetValues("request-id", out IEnumerable<string>? values) ? values.SingleOrDefault() : null;

        if (!response.IsSuccessStatusCode)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning("Anthropic Messages API returned status {StatusCode} with request ID {RequestId}.", (int)response.StatusCode, requestId);
            }
            throw new HttpRequestException("Anthropic rejected the request.", null, response.StatusCode);
        }

        AnthropicMessageResponse? message = await response.Content.ReadFromJsonAsync<AnthropicMessageResponse>(cancellationToken);
        if (message is null)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning("Anthropic Messages API returned an empty response with request ID {RequestId}.", requestId);
            }
            throw new HttpRequestException("Anthropic returned an empty response.", null, response.StatusCode);
        }

        return message;
    }
}
