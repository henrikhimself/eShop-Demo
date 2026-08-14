// <copyright file="KeycloakAdminApiClientTests.cs" company="Henrik Jensen">
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

namespace Hj.EShop.AppHost.Tests;

// Focused unit tests for the field-lookup helper only, not the rest of
// KeycloakAdminApiClient - everything else talks to a real Keycloak admin API and is
// only exercised by the E2E suite against a running container.
public sealed class KeycloakAdminApiClientTests
{
    [Fact]
    public void GetRequiredField_FieldPresent_ReturnsItsStringValue()
    {
        Dictionary<string, object> source = new() { ["access_token"] = "a-token-value" };

        string result = KeycloakAdminApiClient.GetRequiredField(source, "access_token", "a test response");

        Assert.Equal("a-token-value", result);
    }

    [Fact]
    public void GetRequiredField_FieldMissing_ThrowsWithFieldNameContextAndRawResponse()
    {
        // Missing fields should throw an informative error that names the field, the
        // call context, and the raw response body.
        Dictionary<string, object> source = new() { ["token_type"] = "Bearer" };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => KeycloakAdminApiClient.GetRequiredField(source, "access_token", "a test response"));

        Assert.Contains("access_token", exception.Message, StringComparison.Ordinal);
        Assert.Contains("a test response", exception.Message, StringComparison.Ordinal);
        Assert.Contains("token_type", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRequiredField_SourceIsNull_ThrowsWithFieldNameAndContext()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => KeycloakAdminApiClient.GetRequiredField(null, "id", "a test response"));

        Assert.Contains("id", exception.Message, StringComparison.Ordinal);
        Assert.Contains("a test response", exception.Message, StringComparison.Ordinal);
    }
}
