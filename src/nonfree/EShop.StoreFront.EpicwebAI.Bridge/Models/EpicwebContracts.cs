// <copyright file="EpicwebContracts.cs" company="Henrik Jensen">
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
using System.Text.Json.Serialization;

namespace Hj.EShop.StoreFront.EpicwebAI.Bridge.Models;

internal sealed class EpicwebAiRequest
{
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    [JsonPropertyName("assistantInstructions")]
    public string? AssistantInstructions { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("visionmodel")]
    public string? VisionModel { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("requireTools")]
    public bool? RequireTools { get; init; }

    [JsonPropertyName("tools")]
    public IReadOnlyList<EpicwebTool>? Tools { get; init; }

    [JsonPropertyName("toolcalls")]
    public IReadOnlyList<EpicwebToolResult>? ToolCalls { get; init; }

    [JsonPropertyName("chatHistory")]
    public IReadOnlyList<EpicwebChatMessage>? ChatHistory { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }
}

internal sealed class EpicwebChatSuggestionResponse
{
    [JsonPropertyName("suggestions")]
    public IReadOnlyList<string> Suggestions { get; init; } = [];

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("aiStatus")]
    public string? AiStatus { get; init; }
}

internal sealed class EpicwebTool
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("function")]
    public EpicwebToolFunction? Function { get; init; }
}

internal sealed class EpicwebToolFunction
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; init; }
}

internal sealed class EpicwebToolResult
{
    [JsonPropertyName("call_id")]
    public string? CallId { get; init; }

    [JsonPropertyName("output")]
    public string? Output { get; init; }

    [JsonPropertyName("context")]
    public string? Context { get; init; }

    [JsonPropertyName("input")]
    public string? Input { get; init; }

    [JsonPropertyName("toolName")]
    public string? ToolName { get; init; }
}

internal sealed class EpicwebChatMessage
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("toolOutput")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement ToolOutput { get; init; }

    [JsonPropertyName("toolName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement ToolName { get; init; }

    [JsonPropertyName("toolCallId")]
    public string? ToolCallId { get; init; }
}

internal sealed record class EpicwebAiResponse
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("aiStatus")]
    public string? AiStatus { get; init; }

    [JsonPropertyName("toolCalls")]
    public IReadOnlyList<EpicwebToolCall>? ToolCalls { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    [JsonPropertyName("updatedChatHistory")]
    public IReadOnlyList<EpicwebChatMessage>? UpdatedChatHistory { get; init; }

    [JsonPropertyName("tokenUsage")]
    public EpicwebTokenUsage? TokenUsage { get; init; }
}

internal sealed class EpicwebToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public EpicwebToolCallFunction Function { get; init; } = new();
}

internal sealed class EpicwebToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; init; } = "{}";
}

internal sealed class EpicwebTokenUsage
{
    [JsonPropertyName("currentInputTokens")]
    public int CurrentInputTokens { get; init; }

    [JsonPropertyName("currentOutputTokens")]
    public int CurrentOutputTokens { get; init; }

    [JsonPropertyName("currentTotalTokens")]
    public int CurrentTotalTokens { get; init; }
}
