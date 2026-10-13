// <copyright file="ApiSchemaGenerator.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;

namespace Hj.EShop.Cli.Commands.Generate;

// See doc/adr/0014-bff-openapi-source-of-truth-for-frontend-types.md.
internal sealed class ApiSchemaGenerator(IToolExecutor toolExecutor, RepoPaths paths) : IApiSchemaGenerator
{
    public async Task<ApiSchemaGenerationResult> GenerateAsync(CancellationToken cancellationToken)
    {
        string relativeBffProject = Path.GetRelativePath(paths.Root, paths.SellerPortalBffProject);
        ProcessResult build = await toolExecutor.RunAsync(
            new ToolInvocation("dotnet", ["build", relativeBffProject, "--nologo"], WorkingDirectory: paths.Root),
            cancellationToken);

        if (!build.Succeeded)
        {
            return new ApiSchemaGenerationResult(false, $"Failed to build '{relativeBffProject}'.", build.StandardOutput + build.StandardError);
        }

        if (!File.Exists(paths.SellerPortalBffOpenApiJson))
        {
            return new ApiSchemaGenerationResult(
                false,
                $"Expected OpenAPI document not found at '{paths.SellerPortalBffOpenApiJson}' after building the Bff.",
                string.Empty);
        }

        string relativeWebDir = Path.GetRelativePath(paths.Root, paths.SellerPortalWebDir);
        string openApiJsonFromWebDir = Path.GetRelativePath(paths.SellerPortalWebDir, paths.SellerPortalBffOpenApiJson);
        ProcessResult generate = await toolExecutor.RunAsync(
            new ToolInvocation(
                "pnpm",
                ["--dir", relativeWebDir, "exec", "openapi-typescript", openApiJsonFromWebDir, "-o", "lib/api-schema.d.ts"],
                WorkingDirectory: paths.Root),
            cancellationToken);

        return generate.Succeeded
            ? new ApiSchemaGenerationResult(true, $"Regenerated '{paths.GeneratedApiSchema}'.", string.Empty)
            : new ApiSchemaGenerationResult(false, "Failed to regenerate the TypeScript API schema.", generate.StandardOutput + generate.StandardError);
    }
}
