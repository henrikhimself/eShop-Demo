// <copyright file="GenerateTypesCommand.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Output;
using Spectre.Console.Cli;

namespace Hj.EShop.Cli.Commands.Generate;

internal sealed class GenerateTypesCommand(IOutputSink output, IApiSchemaGenerator generator) : AsyncCommand<DefaultSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DefaultSettings settings, CancellationToken cancellationToken)
    {
        output.Heading(1, "Generate Types Result");

        ApiSchemaGenerationResult result;
        using (output.BeginStep("Generate API types"))
        {
            result = await generator.GenerateAsync(cancellationToken);
        }

        // Settles the spinner before printing results - see IOutputSink.FlushPendingStepsAsync.
        await output.FlushPendingStepsAsync();

        if (!result.Succeeded)
        {
            output.Status(Severity.Failure, result.Message);
            if (!string.IsNullOrEmpty(result.Details))
            {
                output.Code("plain", result.Details);
            }

            return 1;
        }

        output.Status(Severity.Success, result.Message);
        return 0;
    }
}
