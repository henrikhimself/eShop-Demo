// <copyright file="FakeApiSchemaGenerator.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Commands.Generate;

namespace Hj.EShop.Cli.Tests.Fakes;

// Lets FormatCommandTests control the schema regen's outcome/delay without wiring up
// the real ApiSchemaGenerator (which shells out to dotnet build + pnpm).
internal sealed class FakeApiSchemaGenerator(Func<ApiSchemaGenerationResult>? responder = null, TimeSpan delay = default) : IApiSchemaGenerator
{
    public int CallCount { get; private set; }

    public async Task<ApiSchemaGenerationResult> GenerateAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        return responder?.Invoke() ?? new ApiSchemaGenerationResult(true, "Regenerated.", string.Empty);
    }
}
