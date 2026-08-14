// <copyright file="AntiforgeryTestSupport.cs" company="Henrik Jensen">
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

namespace Hj.EShop.SellerPortal.Bff.Tests;

// Drives the same token dance lib/bff-fetch.ts does for a real mutating request: fetch
// the antiforgery cookie, then echo its value back as the X-XSRF-TOKEN header. The
// client must be created with HandleCookies: true so the XSRF-TOKEN cookie set here is
// resent automatically on the caller's next request.
internal static class AntiforgeryTestSupport
{
    public static async Task IssueAntiforgeryTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await client.GetAsync(
            new Uri("/bff/api/antiforgery/token", UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();

        string setCookieHeader = response.Headers.GetValues("Set-Cookie")
            .Single(cookie => cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        string token = setCookieHeader["XSRF-TOKEN=".Length..setCookieHeader.IndexOf(';', StringComparison.Ordinal)];

        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
    }
}
