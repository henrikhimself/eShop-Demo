// <copyright file="ScreenshotCommandIntegrationTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Commands;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Integration;

// Needs real Docker and the eshop-utility:local image - exercises the whole
// ToolExecutor -> ContainerRunner -> real Chromium path, including the runtime
// find-the-binary logic in ScreenshotContainerScript, not just the fakes the unit
// tests use. Uses a data: URL so it needs no dev server.
[Trait("Category", "Integration")]
public sealed class ScreenshotCommandIntegrationTests : IDisposable
{
    private readonly RepoPaths _paths = new(new RepoRootLocator().Find());
    private readonly string _outputPath;

    public ScreenshotCommandIntegrationTests()
    {
        _outputPath = Path.Combine(_paths.TmpDir, $"integration-test-{Guid.NewGuid():N}.png");
    }

    [Fact]
    public async Task ExecuteAsync_RealChromium_ProducesNonEmptyPng()
    {
        ProcessRunner processRunner = new();
        RecordingOutputSink output = new();
        UtilityImageProvisioner imageProvisioner = new(processRunner, _paths, () => output, new UtilityImageSourceHasher(_paths));
        LinuxIdentityProvider identityProvider = new(processRunner);
        ContainerRunner containerRunner = new(processRunner, imageProvisioner, identityProvider, _paths);
        ToolExecutor toolExecutor = new(
            new FakeGlobalOptionsAccessor(new Hj.EShop.Cli.GlobalOptions(ExecutionMode.Auto, Hj.EShop.Cli.Output.OutputMode.Human)),
            new LocalToolLocator(),
            containerRunner,
            processRunner,
            output,
            _paths);
        ScreenshotCommand command = new(output, toolExecutor, _paths);
        ScreenshotSettings settings = new() { Url = "data:text/html,<h1>hello</h1>", OutputPath = _outputPath };

        int exitCode = await ((ICommand<ScreenshotSettings>)command).ExecuteAsync(
            context: null!, settings, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(_outputPath));
        Assert.NotEmpty(await File.ReadAllBytesAsync(_outputPath, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        File.Delete(_outputPath);
    }
}
