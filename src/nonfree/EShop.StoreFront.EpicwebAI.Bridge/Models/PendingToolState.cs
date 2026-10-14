// <copyright file="PendingToolState.cs" company="Henrik Jensen">
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

using System.Text.Json.Serialization;

namespace Hj.EShop.StoreFront.Web.EpicwebAI.Models;

internal sealed class PendingToolState
{
    public List<AnthropicMessage> Messages { get; init; } = [];

    public List<AnthropicToolUseContent> ToolUses { get; init; } = [];

    public List<AnthropicTool>? Tools { get; init; }
}

internal sealed class AnthropicToolUseContent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "tool_use";

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("input")]
    public System.Text.Json.JsonElement Input { get; init; }
}

internal sealed class AnthropicToolResultContent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "tool_result";

    [JsonPropertyName("tool_use_id")]
    public string ToolUseId { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}
