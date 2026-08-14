// <copyright file="ContainerRunnerTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Tests.Fakes;
using Xunit;

namespace Hj.EShop.Cli.Tests.Execution;

// Shares a collection with DockerSocketPathResolverTests so it can't race against that
// suite's ESHOP_DOCKER mutation (see DockerSocketEnvironmentMarker).
[Collection(DockerSocketEnvironmentMarker.Name)]
public sealed class ContainerRunnerTests
{
    [Fact]
    public async Task RunAsync_LinuxIdentityPresent_AddsUserAndGroupAddFlags()
    {
        FakeProcessRunner processRunner = new();
        RepoPaths paths = new("/repo");
        ContainerRunner runner = new(
            processRunner,
            new FakeUtilityImageProvisioner(),
            new FakeLinuxIdentityProvider(new LinuxIdentity(1000, 1000, 999)),
            paths);
        ToolInvocation invocation = new("dotnet", ["build"]);

        await runner.RunAsync(invocation, TestContext.Current.CancellationToken);

        ProcessRequest request = Assert.Single(processRunner.Invocations);
        Assert.Equal("docker", request.FileName);
        Assert.Contains("--user", request.Arguments);
        Assert.Contains("1000:1000", request.Arguments);
        Assert.Contains("--group-add", request.Arguments);
        Assert.Contains("999", request.Arguments);
        Assert.Contains(RepoPaths.UtilityImageTag, request.Arguments);
        Assert.Contains("dotnet", request.Arguments);
        Assert.Contains("build", request.Arguments);
        Assert.Contains($"{paths.Root}:/workspace", request.Arguments);
        Assert.Contains("/var/run/docker.sock:/var/run/docker.sock", request.Arguments);
        Assert.Contains("DOCKER_HOST=unix:///var/run/docker.sock", request.Arguments);
    }

    [Fact]
    public async Task RunAsync_CustomDockerSocketPath_PassesMatchingDockerHost()
    {
        Environment.SetEnvironmentVariable("ESHOP_DOCKER", "/run/user/1000/docker.sock");
        try
        {
            FakeProcessRunner processRunner = new();
            ContainerRunner runner = new(
                processRunner,
                new FakeUtilityImageProvisioner(),
                new FakeLinuxIdentityProvider(identity: null),
                new RepoPaths("/repo"));

            await runner.RunAsync(new ToolInvocation("dotnet", ["build"]), TestContext.Current.CancellationToken);

            ProcessRequest request = Assert.Single(processRunner.Invocations);
            Assert.Contains("/run/user/1000/docker.sock:/run/user/1000/docker.sock", request.Arguments);
            Assert.Contains("DOCKER_HOST=unix:///run/user/1000/docker.sock", request.Arguments);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ESHOP_DOCKER", null);
        }
    }

    [Fact]
    public async Task RunAsync_NoLinuxIdentity_OmitsUserAndGroupAddFlags()
    {
        FakeProcessRunner processRunner = new();
        ContainerRunner runner = new(
            processRunner,
            new FakeUtilityImageProvisioner(),
            new FakeLinuxIdentityProvider(identity: null),
            new RepoPaths("/repo"));
        ToolInvocation invocation = new("pnpm", ["install"]);

        await runner.RunAsync(invocation, TestContext.Current.CancellationToken);

        ProcessRequest request = Assert.Single(processRunner.Invocations);
        Assert.DoesNotContain("--user", request.Arguments);
        Assert.DoesNotContain("--group-add", request.Arguments);
    }

    [Fact]
    public async Task RunAsync_NonInteractive_OmitsTtyFlags()
    {
        FakeProcessRunner processRunner = new();
        ContainerRunner runner = new(
            processRunner,
            new FakeUtilityImageProvisioner(),
            new FakeLinuxIdentityProvider(identity: null),
            new RepoPaths("/repo"));

        await runner.RunAsync(new ToolInvocation("dotnet", ["build"]), TestContext.Current.CancellationToken);

        ProcessRequest request = Assert.Single(processRunner.Invocations);
        Assert.DoesNotContain("-i", request.Arguments);
        Assert.DoesNotContain("-t", request.Arguments);
    }

    [Fact]
    public async Task RunAsync_InvocationHasEnvironmentVariables_PassesThemAsDashEFlags()
    {
        FakeProcessRunner processRunner = new();
        ContainerRunner runner = new(
            processRunner,
            new FakeUtilityImageProvisioner(),
            new FakeLinuxIdentityProvider(identity: null),
            new RepoPaths("/repo"));
        ToolInvocation invocation = new("dotnet", ["build"], EnvironmentVariables: new Dictionary<string, string> { ["NO_COLOR"] = "1" });

        await runner.RunAsync(invocation, TestContext.Current.CancellationToken);

        ProcessRequest request = Assert.Single(processRunner.Invocations);
        Assert.Contains("-e", request.Arguments);
        Assert.Contains("NO_COLOR=1", request.Arguments);
    }

    [Fact]
    public async Task RunAsync_EnsuresUtilityImageBeforeRunning()
    {
        FakeProcessRunner processRunner = new();
        FakeUtilityImageProvisioner imageProvisioner = new();
        ContainerRunner runner = new(
            processRunner,
            imageProvisioner,
            new FakeLinuxIdentityProvider(identity: null),
            new RepoPaths("/repo"));

        await runner.RunAsync(new ToolInvocation("dotnet", ["build"]), TestContext.Current.CancellationToken);

        Assert.True(imageProvisioner.EnsureBuiltCalled);
    }

    private sealed class FakeUtilityImageProvisioner : IUtilityImageProvisioner
    {
        public bool EnsureBuiltCalled { get; private set; }

        public Task EnsureBuiltAsync(CancellationToken cancellationToken)
        {
            EnsureBuiltCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLinuxIdentityProvider(LinuxIdentity? identity) : ILinuxIdentityProvider
    {
        public Task<LinuxIdentity?> GetIdentityAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(identity);
        }
    }
}
