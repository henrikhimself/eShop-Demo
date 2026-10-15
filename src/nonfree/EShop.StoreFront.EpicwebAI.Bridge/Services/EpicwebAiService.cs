// <copyright file="EpicwebAiService.cs" company="Henrik Jensen">
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
using System.Text.Json;
using Hj.EShop.StoreFront.EpicwebAI.Bridge.Configuration;
using Hj.EShop.StoreFront.EpicwebAI.Bridge.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Hj.EShop.StoreFront.EpicwebAI.Bridge.Services;

internal sealed class EpicwebAiService(
    AnthropicMessagesClient anthropicMessagesClient,
    IDistributedCache cache,
    IOptions<AnthropicOptions> options)
{
    private static readonly DistributedCacheEntryOptions _toolStateCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
    };

    private readonly AnthropicOptions _options = options.Value;

    public static EpicwebChatSuggestionResponse CreateChatSuggestions(EpicwebAiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.VisionModel) || !string.IsNullOrWhiteSpace(request.Image))
        {
            return new EpicwebChatSuggestionResponse
            {
                Success = false,
                AiStatus = "Image generation and vision are not supported.",
            };
        }

        // Epicweb calls this optional endpoint to populate prompt-starter UI. It is not a
        // content-generation request, so avoid consuming an Anthropic request here.
        return new EpicwebChatSuggestionResponse { Success = true };
    }

    public async Task<EpicwebAiResponse> CreateChatAsync(EpicwebAiRequest request, CancellationToken cancellationToken)
    {
        EpicwebAiRequestValidator.Validate(request);

        string? cacheKey = GetToolStateCacheKey(request);
        PendingToolState? pendingState = cacheKey is null ? null : await GetPendingToolStateAsync(cacheKey, cancellationToken);

        List<AnthropicMessage> messages = pendingState is null
            ? BuildChatMessages(request)
            : BuildToolResultMessages(pendingState, request);
        List<AnthropicTool>? tools = pendingState?.Tools ?? BuildTools(request);
        if (tools is { Count: > 0 } && cacheKey is null)
        {
            throw new ArgumentException("Tool-enabled AI chat requires a session identifier.", nameof(request));
        }

        AnthropicMessageResponse response = await anthropicMessagesClient.CreateMessageAsync(
            new()
            {
                Model = AnthropicOptions.Model,
                MaxTokens = _options.MaxOutputTokens,
                System = request.AssistantInstructions,
                Messages = messages,
                Tools = tools,
            },
            cancellationToken);

        List<AnthropicToolUseContent> toolUses = BuildToolUses(response);
        if (cacheKey is not null)
        {
            if (toolUses.Count > 0)
            {
                PendingToolState state = new() { Messages = messages, ToolUses = toolUses, Tools = tools };
                await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(state), _toolStateCacheOptions, cancellationToken);
            }
            else if (pendingState is not null)
            {
                await cache.RemoveAsync(cacheKey, cancellationToken);
            }
        }

        EpicwebAiResponse result = CreateResponse(request, response, toolUses);
        EpicwebChatMessage assistantMessage = new()
        {
            Id = response.Id,
            Role = "assistant",
            Content = result.Text,
            Timestamp = DateTimeOffset.UtcNow,
        };

        return result with
        {
            UpdatedChatHistory = [.. request.ChatHistory ?? [], assistantMessage],
        };
    }

    public async Task<EpicwebAiResponse> CreateSuggestionAsync(EpicwebAiRequest request, CancellationToken cancellationToken)
    {
        EpicwebAiRequestValidator.Validate(request);

        AnthropicMessageResponse response = await anthropicMessagesClient.CreateMessageAsync(
            new()
            {
                Model = AnthropicOptions.Model,
                MaxTokens = _options.MaxOutputTokens,
                System = request.AssistantInstructions,
                Messages = [new AnthropicMessage { Role = "user", Content = GetPrompt(request) }],
            },
            cancellationToken);

        return CreateResponse(request, response, []);
    }

    private static List<AnthropicToolUseContent> BuildToolUses(AnthropicMessageResponse response)
    {
        return [.. response.Content
            .Where(block => string.Equals(block.Type, "tool_use", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(block.Id) && !string.IsNullOrWhiteSpace(block.Name))
            .Select(block => new AnthropicToolUseContent { Id = block.Id!, Name = block.Name!, Input = block.Input })];
    }

    private static List<AnthropicTool>? BuildTools(EpicwebAiRequest request)
    {
        if (request.Tools is not { Count: > 0 })
        {
            return null;
        }

        return [.. request.Tools
            .Where(tool => tool.Function is not null && !string.IsNullOrWhiteSpace(tool.Function.Name) && tool.Function.Parameters.ValueKind == JsonValueKind.Object)
            .Select(tool => new AnthropicTool
            {
                Name = tool.Function!.Name!,
                Description = tool.Function.Description,
                InputSchema = tool.Function.Parameters,
            })];
    }

    private static List<AnthropicMessage> BuildToolResultMessages(PendingToolState state, EpicwebAiRequest request)
    {
        List<EpicwebChatMessage> availableResults = [.. (request.ChatHistory ?? [])
            .Where(message => message.ToolName.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)];
        List<EpicwebChatMessage> matchedResults = [];

        foreach (AnthropicToolUseContent toolUse in state.ToolUses)
        {
            int resultIndex = availableResults.FindIndex(result => string.Equals(GetToolName(result), toolUse.Name, StringComparison.Ordinal));
            if (resultIndex < 0)
            {
                throw new ArgumentException("The AI tool result does not match the pending tool call.", nameof(request));
            }

            matchedResults.Add(availableResults[resultIndex]);
            availableResults.RemoveAt(resultIndex);
        }

        List<AnthropicMessage> messages = [.. state.Messages];
        messages.Add(new AnthropicMessage { Role = "assistant", Content = state.ToolUses });
        messages.Add(new AnthropicMessage
        {
            Role = "user",
            Content = state.ToolUses.Select((toolUse, index) => new AnthropicToolResultContent
            {
                ToolUseId = toolUse.Id,
                Content = GetToolResultContent(matchedResults[index]),
            }).ToList(),
        });

        return messages;
    }

    private static EpicwebAiResponse CreateResponse(EpicwebAiRequest request, AnthropicMessageResponse response, List<AnthropicToolUseContent> toolUses)
    {
        string text = string.Concat(response.Content
            .Where(block => string.Equals(block.Type, "text", StringComparison.Ordinal))
            .Select(block => block.Text));

        return new()
        {
            Text = text,
            MessageId = response.Id,
            SessionId = request.SessionId,
            ToolCalls = [.. toolUses.Select(toolUse => new EpicwebToolCall
            {
                Id = toolUse.Id,
                Function = new EpicwebToolCallFunction
                {
                    Name = toolUse.Name,
                    Arguments = toolUse.Input.GetRawText(),
                },
            })],
            TokenUsage = response.Usage is null
                ? null
                : new()
                {
                    CurrentInputTokens = response.Usage.InputTokens,
                    CurrentOutputTokens = response.Usage.OutputTokens,
                    CurrentTotalTokens = response.Usage.InputTokens + response.Usage.OutputTokens,
                },
        };
    }

    private static List<AnthropicMessage> BuildChatMessages(EpicwebAiRequest request)
    {
        List<AnthropicMessage> messages = [.. (request.ChatHistory ?? [])
            .Where(message => !string.IsNullOrWhiteSpace(message.Content) && message.ToolName.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            .Select(message => new AnthropicMessage
            {
                Role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
                Content = message.Content!,
            })];

        string prompt = GetPrompt(request);
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            messages.Add(new AnthropicMessage { Role = "user", Content = prompt });
        }

        return messages;
    }

    private static string? GetToolStateCacheKey(EpicwebAiRequest request)
    {
        string? sessionId = request.SessionId ?? request.CorrelationId;
        return string.IsNullOrWhiteSpace(sessionId) ? null : $"epicweb-ai:tools:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)))}";
    }

    private static string GetToolName(EpicwebChatMessage message)
    {
        return message.ToolName.ValueKind == JsonValueKind.String ? message.ToolName.GetString() ?? string.Empty : string.Empty;
    }

    private static string GetToolResultContent(EpicwebChatMessage message)
    {
        return message.ToolOutput.ValueKind == JsonValueKind.String ? message.ToolOutput.GetString() ?? string.Empty : message.ToolOutput.GetRawText();
    }

    private async Task<PendingToolState?> GetPendingToolStateAsync(string cacheKey, CancellationToken cancellationToken)
    {
        string? serialized = await cache.GetStringAsync(cacheKey, cancellationToken);
        return string.IsNullOrEmpty(serialized) ? null : JsonSerializer.Deserialize<PendingToolState>(serialized);
    }

    private static string GetPrompt(EpicwebAiRequest request)
    {
        return string.Join(
            Environment.NewLine,
            new[] { request.Prompt, request.Text }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
