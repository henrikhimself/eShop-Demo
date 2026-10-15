// <copyright file="ServiceCollectionExtensions.cs" company="Henrik Jensen">
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

using Hj.EShop.Common;
using Hj.EShop.StoreFront.EpicwebAI.Bridge.Configuration;
using Hj.EShop.StoreFront.EpicwebAI.Bridge.Services;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Hj.EShop.StoreFront.EpicwebAI.Bridge;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEpicwebAiBridge(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AnthropicOptions>()
            .Bind(configuration.GetSection(AnthropicOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "Anthropic:ApiKey must be configured.")
            .Validate(
                options => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri? endpoint) && endpoint.Scheme == Uri.UriSchemeHttps,
                "Anthropic:Endpoint must be an absolute HTTPS URI.")
            .Validate(
                options => options.MaxOutputTokens is > 0 and <= 8_192,
                "Anthropic:MaxOutputTokens must be between 1 and 8192.")
            .Validate(
                options => options.MaxRequestBytes is > 0 and <= 1_048_576,
                "Anthropic:MaxRequestBytes must be between 1 and 1048576.")
            .Validate(
                options => options.MaxToolIterations is > 0 and <= 16,
                "Anthropic:MaxToolIterations must be between 1 and 16.")
            .ValidateOnStart();

        services.AddStackExchangeRedisCache(cacheOptions =>
            cacheOptions.Configuration = configuration.GetConnectionString(KnownNames.ResourceCache));
        services.AddScoped<EpicwebAiService>();

        services.AddHttpClient<AnthropicMessagesClient>((serviceProvider, client) =>
            {
                client.BaseAddress = new Uri(serviceProvider.GetRequiredService<IOptions<AnthropicOptions>>().Value.Endpoint);
                client.Timeout = TimeSpan.FromMinutes(2);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("eshop-epicweb-ai-bridge/1.0");
            });
        services.Configure<HttpStandardResilienceOptions>(
            $"{nameof(AnthropicMessagesClient)}-standard",
            options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
                options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            });

        return services;
    }
}
