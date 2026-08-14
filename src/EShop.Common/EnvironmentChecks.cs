// <copyright file="EnvironmentChecks.cs" company="Henrik Jensen">
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

using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace Hj.EShop.Common;

public static class EnvironmentChecks
{
    public const string TestingEnvironmentName = "Testing";

    // See doc/adr/0014-bff-openapi-source-of-truth-for-frontend-types.md — build-time
    // OpenAPI generation runs through a mock host (GetDocument.Insider).
    public static bool IsBuildTimeOpenApiGeneration =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    public static bool IsTestingEnvironment(this IHostEnvironment environment)
    {
        return environment.IsEnvironment(TestingEnvironmentName);
    }
}
