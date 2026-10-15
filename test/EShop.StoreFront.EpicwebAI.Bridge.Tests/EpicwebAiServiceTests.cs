// <copyright file="EpicwebAiServiceTests.cs" company="Henrik Jensen">
// Copyright 2026 Henrik Jensen
//
// Licensed under the Apache License, Version 2.0 (the "License");
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
using Hj.EShop.StoreFront.EpicwebAI.Bridge.Configuration;
using Hj.EShop.StoreFront.EpicwebAI.Bridge.Models;
using Hj.EShop.StoreFront.EpicwebAI.Bridge.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hj.EShop.StoreFront.EpicwebAI.Bridge.Tests;

public sealed class EpicwebAiServiceTests
{
    [Fact]
    public void CreateChatSuggestions_ReturnsSuccessfulEmptyPromptStartersWithoutCallingAnthropic()
    {
        RecordingHandler handler = new("{}");
        EpicwebChatSuggestionResponse response = EpicwebAiService.CreateChatSuggestions(new EpicwebAiRequest());
        Assert.True(response.Success);
        Assert.Empty(response.Suggestions);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public void EpicwebAiResponse_SerializesChatHistoryWithoutToolData()
    {
        EpicwebAiResponse response = new()
        {
            UpdatedChatHistory = [new EpicwebChatMessage { Role = "assistant", Content = "Hello" }],
        };

        string serialized = JsonSerializer.Serialize(response);

        Assert.Contains("\"content\":\"Hello\"", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("toolOutput", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("toolName", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateSuggestionAsync_MapsPromptAndTextResponse()
    {
        RecordingHandler handler = new("""{"id":"msg-1","content":[{"type":"text","text":"Hello editor"}],"usage":{"input_tokens":4,"output_tokens":2}}""");
        EpicwebAiService service = CreateService(handler, new TestDistributedCache());

        EpicwebAiResponse response = await service.CreateSuggestionAsync(new EpicwebAiRequest { Prompt = "Write a title" }, TestContext.Current.CancellationToken);

        Assert.Equal("Hello editor", response.Text);
        Assert.Equal("msg-1", response.MessageId);
        Assert.Equal(4, response.TokenUsage!.CurrentInputTokens);
        Assert.Equal(2, response.TokenUsage.CurrentOutputTokens);
        Assert.Equal("test-key", handler.ApiKey);
        Assert.Equal("2023-06-01", handler.ApiVersion);
        Assert.Contains("Write a title", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateSuggestionAsync_RejectsImageAndVisionRequestsWithoutCallingAnthropic()
    {
        RecordingHandler handler = new("{}");
        EpicwebAiService service = CreateService(handler, new TestDistributedCache());

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateSuggestionAsync(new EpicwebAiRequest { Prompt = "draw", Image = "image-data" }, TestContext.Current.CancellationToken));

        Assert.StartsWith("Image generation and vision are not supported.", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task CreateSuggestionAsync_DoesNotExposeProviderErrorContent()
    {
        RecordingHandler handler = new("""{"error":{"message":"provider secret detail"}}""", HttpStatusCode.BadGateway);
        EpicwebAiService service = CreateService(handler, new TestDistributedCache());

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.CreateSuggestionAsync(new EpicwebAiRequest { Prompt = "Write a title" }, TestContext.Current.CancellationToken));

        Assert.Equal("Anthropic rejected the request.", exception.Message);
        Assert.DoesNotContain("provider secret detail", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateChatAsync_PreservesToolUseAndMapsLaterToolResult()
    {
        RecordingHandler handler = new(
            """{"id":"msg-tool","content":[{"type":"tool_use","id":"tool-1","name":"ReadContent","input":{"contentId":"42"}}],"usage":{"input_tokens":5,"output_tokens":1}}""",
            """{"id":"msg-final","content":[{"type":"text","text":"The title is Example."}],"usage":{"input_tokens":8,"output_tokens":3}}""",
            """{"id":"msg-next","content":[{"type":"text","text":"You can publish the page."}],"usage":{"input_tokens":9,"output_tokens":4}}""");
        EpicwebAiService service = CreateService(handler, new TestDistributedCache());
        EpicwebAiRequest initialRequest = new()
        {
            SessionId = "session-1",
            Text = "Read this page",
            Tools =
            [
                new EpicwebTool
                {
                    Function = new EpicwebToolFunction
                    {
                        Name = "ReadContent",
                        Description = "Reads content",
                        Parameters = JsonDocument.Parse("""{"type":"object","properties":{"contentId":{"type":"string"}}}""").RootElement.Clone(),
                    },
                },
            ],
        };

        EpicwebAiResponse toolResponse = await service.CreateChatAsync(initialRequest, TestContext.Current.CancellationToken);
        EpicwebChatMessage toolResult = new()
        {
            Id = "Epicweb-generated-tool-result-id",
            Role = "system",
            ToolName = JsonSerializer.SerializeToElement("ReadContent"),
            ToolOutput = JsonDocument.Parse("""{"title":"Example"}""").RootElement.Clone(),
        };
        EpicwebAiResponse finalResponse = await service.CreateChatAsync(
            new EpicwebAiRequest { SessionId = "session-1", ChatHistory = [toolResult] },
            TestContext.Current.CancellationToken);
        EpicwebAiResponse laterResponse = await service.CreateChatAsync(
            new EpicwebAiRequest
            {
                SessionId = "session-1",
                Text = "What can I do next?",
                ChatHistory =
                [
                    toolResult,
                    new EpicwebChatMessage { Id = finalResponse.MessageId, Role = "assistant", Content = finalResponse.Text },
                ],
            },
            TestContext.Current.CancellationToken);

        Assert.Single(toolResponse.ToolCalls!);
        Assert.Equal("ReadContent", toolResponse.ToolCalls![0].Function.Name);
        Assert.Equal("The title is Example.", finalResponse.Text);
        Assert.Contains("tool_result", handler.RequestBodies[1], StringComparison.Ordinal);
        Assert.Contains("tool_use_id", handler.RequestBodies[1], StringComparison.Ordinal);
        Assert.Equal("You can publish the page.", laterResponse.Text);
    }

    private static EpicwebAiService CreateService(RecordingHandler handler, IDistributedCache cache)
    {
        AnthropicOptions options = new() { ApiKey = "test-key", Endpoint = "https://test.invalid/" };
        HttpClient httpClient = new(handler) { BaseAddress = new Uri(options.Endpoint) };
        AnthropicMessagesClient client = new(httpClient, Options.Create(options), NullLogger<AnthropicMessagesClient>.Instance);
        return new EpicwebAiService(client, cache, Options.Create(options));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;
        private readonly HttpStatusCode _statusCode;

        public RecordingHandler(params string[] responses)
        {
            _responses = new Queue<string>(responses);
            _statusCode = HttpStatusCode.OK;
        }

        public RecordingHandler(string response, HttpStatusCode statusCode)
        {
            _responses = new Queue<string>([response]);
            _statusCode = statusCode;
        }


        public string ApiKey { get; private set; } = string.Empty;

        public string ApiVersion { get; private set; } = string.Empty;

        public int CallCount { get; private set; }

        public string RequestBody => RequestBodies.Single();

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            ApiKey = request.Headers.GetValues("x-api-key").Single();
            ApiVersion = request.Headers.GetValues("anthropic-version").Single();
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(_statusCode) { Content = new StringContent(_responses.Dequeue()) };
        }
    }

    private sealed class TestDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> values = [];

        public byte[]? Get(string key)
        {
            return values.GetValueOrDefault(key);
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            return Task.FromResult(Get(key));
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            values.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            values[key] = value;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }
}
