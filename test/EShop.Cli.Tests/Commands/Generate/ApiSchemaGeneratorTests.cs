// <copyright file="ApiSchemaGeneratorTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands.Generate;

public sealed class ApiSchemaGeneratorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("eshop-cli-tests-").FullName;
    private readonly RepoPaths _paths;

    public ApiSchemaGeneratorTests()
    {
        _paths = new RepoPaths(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.SellerPortalBffOpenApiJson)!);
    }

    [Fact]
    public async Task GenerateAsync_BuildFails_ReturnsFailureWithoutRunningOpenApiTypescript()
    {
        FakeToolExecutor toolExecutor = new(invocation => invocation.Tool == "dotnet"
            ? new ProcessResult(1, string.Empty, "build error")
            : new ProcessResult(0, string.Empty, string.Empty));
        ApiSchemaGenerator generator = new(toolExecutor, _paths);

        ApiSchemaGenerationResult result = await generator.GenerateAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.DoesNotContain(toolExecutor.Invocations, invocation => invocation.Tool == "pnpm");
    }

    [Fact]
    public async Task GenerateAsync_OpenApiDocumentMissingAfterBuild_ReturnsFailure()
    {
        FakeToolExecutor toolExecutor = new();
        ApiSchemaGenerator generator = new(toolExecutor, _paths);

        ApiSchemaGenerationResult result = await generator.GenerateAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_BuildAndGenerateSucceed_ReturnsSuccess()
    {
        await File.WriteAllTextAsync(_paths.SellerPortalBffOpenApiJson, "{}", TestContext.Current.CancellationToken);
        FakeToolExecutor toolExecutor = new();
        ApiSchemaGenerator generator = new(toolExecutor, _paths);

        ApiSchemaGenerationResult result = await generator.GenerateAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains(toolExecutor.Invocations, invocation => invocation.Tool == "pnpm" && invocation.Arguments.Contains("openapi-typescript"));
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
