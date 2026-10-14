// <copyright file="EndpointRouteBuilderExtensions.cs" company="Henrik Jensen">
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

using Hj.EShop.StoreFront.Web.EpicwebAI.Configuration;
using Hj.EShop.StoreFront.Web.EpicwebAI.Models;
using Hj.EShop.StoreFront.Web.EpicwebAI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hj.EShop.StoreFront.Web.EpicwebAI;

internal static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapEpicwebAiBridge(this IEndpointRouteBuilder endpoints)
    {
        AnthropicOptions options = endpoints.ServiceProvider.GetRequiredService<IOptions<AnthropicOptions>>().Value;

        endpoints.MapPost("/api/AiSuggestion", CreateSuggestionAsync)
            .WithMetadata(new RequestSizeLimitAttribute(options.MaxRequestBytes));
        endpoints.MapPost("/api/AiChat", CreateChatAsync)
            .WithMetadata(new RequestSizeLimitAttribute(options.MaxRequestBytes));
        endpoints.MapPost("/api/AiChatSuggestions", CreateChatSuggestions)
            .WithMetadata(new RequestSizeLimitAttribute(options.MaxRequestBytes));

        return endpoints;
    }

    private static IResult CreateChatSuggestions(EpicwebAiRequest request)
    {
        return Results.Ok(EpicwebAiService.CreateChatSuggestions(request));
    }

    private static async Task<IResult> CreateChatAsync(
        EpicwebAiRequest request,
        EpicwebAiService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            EpicwebAiResponse response = await service.CreateChatAsync(request, cancellationToken);
            ILogger logger = loggerFactory.CreateLogger("EpicwebAiBridge");
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "AiChat completed with {TextLength} response characters, {ToolCallCount} tool calls, and {HistoryCount} chat history entries.",
                    response.Text.Length,
                    response.ToolCalls?.Count ?? 0,
                    response.UpdatedChatHistory?.Count ?? 0);
            }
            return Results.Ok(response);
        }
        catch (ArgumentException exception)
        {
            return Results.Ok(new EpicwebAiResponse { AiStatus = exception.Message });
        }
        catch (HttpRequestException)
        {
            return Results.Ok(new EpicwebAiResponse { AiStatus = "The AI provider is currently unavailable. Please try again." });
        }
    }

    private static async Task<IResult> CreateSuggestionAsync(
        EpicwebAiRequest request,
        EpicwebAiService service,
        CancellationToken cancellationToken)
    {
        try
        {
            EpicwebAiResponse response = await service.CreateSuggestionAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException exception)
        {
            return Results.Ok(new EpicwebAiResponse { AiStatus = exception.Message });
        }
        catch (HttpRequestException)
        {
            return Results.Ok(new EpicwebAiResponse { AiStatus = "The AI provider is currently unavailable. Please try again." });
        }
    }
}
