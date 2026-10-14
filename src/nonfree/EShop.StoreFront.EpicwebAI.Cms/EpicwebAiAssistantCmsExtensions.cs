// <copyright file="EpicwebAiAssistantCmsExtensions.cs" company="Henrik Jensen">
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

using Epicweb.Optimizely.AIAssistant;
using Epicweb.Optimizely.AIAssistant.Tools;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hj.EShop.StoreFront.EpicwebAI.Cms;

public static class EpicwebAiAssistantCmsExtensions
{
    public static IServiceCollection AddEpicwebAiAssistantCms(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAIAssistant(
            setupAction: options =>
            {
                options.ProviderName = "CustomAI";
                options.AIModel = "claude-sonnet-5-5";
                options.EnableChat = true;
                options.ChatRoles = ["WebEditors", "CmsEditors", "WebAdmins", "CmsAdmins"];
                options.MaxToolIterations = 8;
            })
            .RegisterMcpToolType(typeof(BuiltinTools))
            .RegisterMcpToolType(typeof(BuiltinChatTools))
            .RegisterMcpToolType(typeof(BuiltinPublishChatTools))
            .RegisterMcpToolType(typeof(BuiltinUpdateChatTools));

        return services;
    }

    public static IEndpointRouteBuilder MapEpicwebAiAssistantHub(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapAIAssistantHub();

        return endpoints;
    }
}
