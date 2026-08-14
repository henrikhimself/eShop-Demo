// <copyright file="RenderDiagramCommandIntegrationTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Commands.Diagram;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Integration;

// Needs real Docker and the eshop-utility:local image - exercises the whole
// ToolExecutor -> ContainerRunner -> real `plantuml` path, not just the fakes the
// unit tests use.
[Trait("Category", "Integration")]
public sealed class RenderDiagramCommandIntegrationTests : IDisposable
{
    private readonly RepoPaths _paths = new(new RepoRootLocator().Find());
    private readonly string _pumlPath;

    public RenderDiagramCommandIntegrationTests()
    {
        _pumlPath = Path.Combine(_paths.TmpDir, $"integration-test-{Guid.NewGuid():N}.puml");
        File.WriteAllText(_pumlPath, "@startuml\nAlice -> Bob: hello\n@enduml\n");
    }

    [Fact]
    public async Task ExecuteAsync_RealPlantUml_RendersNonEmptySvg()
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
            processRunner);
        RenderDiagramCommand command = new(output, toolExecutor, _paths);
        RenderDiagramSettings settings = new() { File = _pumlPath };

        int exitCode = await ((ICommand<RenderDiagramSettings>)command).ExecuteAsync(
            context: null!, settings, TestContext.Current.CancellationToken);

        string expectedSvgPath = Path.ChangeExtension(_pumlPath, ".svg");
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(expectedSvgPath));
        Assert.NotEmpty(await File.ReadAllTextAsync(expectedSvgPath, TestContext.Current.CancellationToken));

        File.Delete(expectedSvgPath);
    }

    public void Dispose()
    {
        File.Delete(_pumlPath);
    }
}
