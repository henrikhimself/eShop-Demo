// <copyright file="EpicwebAiAssistantCmsExtensionsTests.cs" company="Henrik Jensen">
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
using Hj.EShop.StoreFront.EpicwebAI.Cms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hj.EShop.StoreFront.Web.EpicwebAI.Tests;

public sealed class EpicwebAiAssistantCmsExtensionsTests
{
    [Fact]
    public void AddEpicwebAiAssistantCms_ConfiguresFixedOptionsAndBindsApiUrl()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Epicweb:AIAssistant:ApiUrl"] = "http://storefront-epicweb-ai",
                })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);

        services.AddEpicwebAiAssistantCms();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        AIAssistantOptions options = serviceProvider.GetRequiredService<IOptions<AIAssistantOptions>>().Value;

        Assert.Equal("http://storefront-epicweb-ai", options.ApiUrl);
        Assert.Equal("CustomAI", options.ProviderName);
        Assert.Equal("claude-sonnet-5-5", options.AIModel);
        Assert.True(options.EnableChat);
        Assert.Equal(["WebEditors", "CmsEditors", "WebAdmins", "CmsAdmins"], options.ChatRoles);
        Assert.Equal(8, options.MaxToolIterations);
    }
}
