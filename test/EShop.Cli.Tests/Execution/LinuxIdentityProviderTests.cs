// <copyright file="LinuxIdentityProviderTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Tests.Fakes;
using Xunit;

namespace Hj.EShop.Cli.Tests.Execution;

// This test suite only runs on Linux CI/dev machines (see AGENTS.md), so
// RuntimeInformation.IsOSPlatform(OSPlatform.Linux) is always true here - no skip
// logic needed for the "not Linux" branch. Shares a collection with
// DockerSocketPathResolverTests so it can't race against that suite's
// ESHOP_DOCKER mutation (see DockerSocketEnvironmentMarker).
[Collection(DockerSocketEnvironmentMarker.Name)]
public sealed class LinuxIdentityProviderTests
{
    [Fact]
    public async Task GetIdentityAsync_OnLinux_ReturnsRealUidAndGid()
    {
        FakeProcessRunner processRunner = new(_ => new ProcessResult(0, "999\n", string.Empty));
        LinuxIdentityProvider provider = new(processRunner);

        LinuxIdentity? identity = await provider.GetIdentityAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(identity);
        Assert.True(identity.Uid >= 0);
        Assert.True(identity.Gid >= 0);
        Assert.Equal(999, identity.DockerSocketGid);
    }

    [Fact]
    public async Task GetIdentityAsync_StatFails_DockerSocketGidIsNull()
    {
        FakeProcessRunner processRunner = new(_ => new ProcessResult(1, string.Empty, "stat: cannot stat"));
        LinuxIdentityProvider provider = new(processRunner);

        LinuxIdentity? identity = await provider.GetIdentityAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(identity);
        Assert.Null(identity.DockerSocketGid);
    }

    [Fact]
    public async Task GetIdentityAsync_RootlessDocker_ReturnsRootIdentityWithNoSocketGid()
    {
        FakeProcessRunner processRunner = new(request => request.FileName == "docker"
            ? new ProcessResult(0, "[name=seccomp,profile=builtin name=rootless name=cgroupns]\n", string.Empty)
            : new ProcessResult(0, "999\n", string.Empty));
        LinuxIdentityProvider provider = new(processRunner);

        LinuxIdentity? identity = await provider.GetIdentityAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(identity);
        Assert.Equal(0, identity.Uid);
        Assert.Equal(0, identity.Gid);
        Assert.Null(identity.DockerSocketGid);
    }

    [Fact]
    public async Task GetIdentityAsync_NonRootlessDocker_ResolvesRealUidGidAndSocketGid()
    {
        FakeProcessRunner processRunner = new(request => request.FileName == "docker"
            ? new ProcessResult(0, "[name=seccomp,profile=builtin]\n", string.Empty)
            : new ProcessResult(0, "999\n", string.Empty));
        LinuxIdentityProvider provider = new(processRunner);

        LinuxIdentity? identity = await provider.GetIdentityAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(identity);
        Assert.True(identity.Uid >= 0);
        Assert.True(identity.Gid >= 0);
        Assert.Equal(999, identity.DockerSocketGid);
    }
}
