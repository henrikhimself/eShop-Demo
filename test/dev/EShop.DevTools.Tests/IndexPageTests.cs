// <copyright file="IndexPageTests.cs" company="Henrik Jensen">
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

using Xunit;

namespace Hj.EShop.DevTools.Tests;

public sealed class IndexPageTests
{
    [Fact]
    public async Task GetIndex_ReturnsOkAndLinksToTheSimulatorTool()
    {
        using DevToolsWebApplicationFactory factory = new();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        string html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("/Tools/SellerDraftApprovalSimulator", html, StringComparison.Ordinal);
        Assert.Contains("/Tools/SellerInventoryReportSimulator", html, StringComparison.Ordinal);
    }
}
