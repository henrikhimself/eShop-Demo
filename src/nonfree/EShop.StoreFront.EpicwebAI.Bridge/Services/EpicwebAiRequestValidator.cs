// <copyright file="EpicwebAiRequestValidator.cs" company="Henrik Jensen">
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

namespace Hj.EShop.StoreFront.EpicwebAI.Bridge.Services;

internal static class EpicwebAiRequestValidator
{
    public static void Validate(EpicwebAiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.Model) && !string.Equals(request.Model, AnthropicOptions.Model, StringComparison.Ordinal))
        {
            throw new ArgumentException("The requested AI model is not supported.", nameof(request));
        }

        if (!string.IsNullOrWhiteSpace(request.VisionModel) || !string.IsNullOrWhiteSpace(request.Image) || IsUnsupportedVisualAction(request.Action))
        {
            throw new ArgumentException("Image generation and vision are not supported.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.Prompt) && request.ChatHistory is not { Count: > 0 } && request.ToolCalls is not { Count: > 0 })
        {
            throw new ArgumentException("The AI request must include text, a prompt, chat history, or a tool result.", nameof(request));
        }
    }

    private static bool IsUnsupportedVisualAction(string? action)
    {
        return action?.Contains("image", StringComparison.OrdinalIgnoreCase) is true
            || action?.Contains("vision", StringComparison.OrdinalIgnoreCase) is true;
    }
}

