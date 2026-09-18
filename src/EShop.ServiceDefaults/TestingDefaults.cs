// <copyright file="TestingDefaults.cs" company="Henrik Jensen">
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

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Hj.EShop.ServiceDefaults;

// Logic and defaults specific to testing and the "Fake" environment - never used in
// Production, and never meant to connect to anything real.
public static class TestingDefaults
{
    // Placeholder values for the build-time OpenAPI-generation mock host (ADR 0014) - satisfy DI binding only, never actually connect.
    public const string FakeSqlServerConnectionString = "Server=fake;Database=fake;TrustServerCertificate=True;";
    public const string FakeServiceBusConnectionString = "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=fake;SharedAccessKey=ZmFrZQ==";
    public const string FakeStorageConnectionString = "UseDevelopmentStorage=true";
    public const string FakeBlobContainerName = "fake";

    // The proxy's self-signed dev CA (PLAN-2.md) is trusted by a developer's own OS/browser (DEVELOP.md), not by HttpClients inside AppHost-launched resources.
    // Gated on non-Production, not IsFakeEnvironment(): Aspire always launches project resources as "Development", even under containerized e2e; "Fake" only applies to WebApplicationFactory unit tests, which never go through the reverse proxy.
    public static HttpClientHandler CreateLenientHttpHandler()
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, chain, errors) =>
                errors == SslPolicyErrors.RemoteCertificateChainErrors
                && chain is not null
                && chain.ChainStatus.All(status => status.Status
                    is X509ChainStatusFlags.UntrustedRoot or X509ChainStatusFlags.PartialChain),
        };
    }
}
