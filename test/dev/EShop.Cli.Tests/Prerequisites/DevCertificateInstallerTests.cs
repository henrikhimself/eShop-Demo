// <copyright file="DevCertificateInstallerTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Prerequisites;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Xunit;

namespace Hj.EShop.Cli.Tests.Prerequisites;

public sealed class DevCertificateInstallerTests : IDisposable
{
    private readonly string _repoRoot = Directory.CreateTempSubdirectory("eshop-cli-tests-").FullName;

    [Fact]
    public async Task EnsureTrustedAsync_NoLocalDotnet_FallsBackToContainerTrustOnly()
    {
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = new(
            new FakeLocalToolLocator(),
            processRunner,
            containerRunner,
            new RepoPaths(_repoRoot));

        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);

        Assert.Empty(processRunner.Invocations);
        ToolInvocation invocation = Assert.Single(containerRunner.Invocations);
        Assert.Equal("dotnet", invocation.Tool);
        Assert.Equal(["dev-certs", "https", "--trust"], invocation.Arguments);
    }

    [Fact]
    public async Task EnsureTrustedAsync_LocalDotnetAndCertNotYetExported_ExportsThenImportsIntoContainer()
    {
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = new(
            new FakeLocalToolLocator("dotnet"),
            processRunner,
            containerRunner,
            new RepoPaths(_repoRoot));

        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);

        ProcessRequest export = Assert.Single(processRunner.Invocations);
        Assert.Equal("dotnet", export.FileName);
        Assert.Contains("--export-path", export.Arguments);

        ToolInvocation import = Assert.Single(containerRunner.Invocations);
        Assert.Equal("sh", import.Tool);
    }

    [Fact]
    public async Task EnsureTrustedAsync_CertAlreadyExported_DoesNothing()
    {
        string devCertPath = Path.Combine(_repoRoot, "tmp", "devcert.pfx");
        Directory.CreateDirectory(Path.GetDirectoryName(devCertPath)!);
        await File.WriteAllTextAsync(devCertPath, "already-exported", TestContext.Current.CancellationToken);

        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = new(
            new FakeLocalToolLocator("dotnet"),
            processRunner,
            containerRunner,
            new RepoPaths(_repoRoot));

        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);

        Assert.Empty(processRunner.Invocations);
        Assert.Empty(containerRunner.Invocations);
    }

    public void Dispose()
    {
        Directory.Delete(_repoRoot, recursive: true);
    }
}
