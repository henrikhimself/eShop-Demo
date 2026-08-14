// <copyright file="DockerSocketPathResolverTests.cs" company="Henrik Jensen">
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
using Xunit;

namespace Hj.EShop.Cli.Tests.Execution;

[Collection(DockerSocketEnvironmentMarker.Name)]
public sealed class DockerSocketPathResolverTests
{
    [Fact]
    public void Resolve_EnvironmentVariableNotSet_ReturnsDefaultPath()
    {
        Environment.SetEnvironmentVariable("ESHOP_DOCKER", null);

        Assert.Equal("/var/run/docker.sock", DockerSocketPathResolver.Resolve());
    }

    [Fact]
    public void Resolve_EnvironmentVariableSet_ReturnsConfiguredPath()
    {
        Environment.SetEnvironmentVariable("ESHOP_DOCKER", "/var/run/user/1000/docker.sock");
        try
        {
            Assert.Equal("/var/run/user/1000/docker.sock", DockerSocketPathResolver.Resolve());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ESHOP_DOCKER", null);
        }
    }

    [Fact]
    public void Resolve_EshopDockerSet_TakesPrecedenceOverDockerHost()
    {
        Environment.SetEnvironmentVariable("ESHOP_DOCKER", "/override/docker.sock");
        Environment.SetEnvironmentVariable("DOCKER_HOST", "unix:///run/user/1000/docker.sock");
        try
        {
            Assert.Equal("/override/docker.sock", DockerSocketPathResolver.Resolve());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ESHOP_DOCKER", null);
            Environment.SetEnvironmentVariable("DOCKER_HOST", null);
        }
    }

    [Fact]
    public void Resolve_DockerHostSetWithUnixScheme_StripsPrefix()
    {
        Environment.SetEnvironmentVariable("DOCKER_HOST", "unix:///run/user/1000/docker.sock");
        try
        {
            Assert.Equal("/run/user/1000/docker.sock", DockerSocketPathResolver.Resolve());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", null);
        }
    }

    [Fact]
    public void Resolve_DockerHostSetWithoutUnixScheme_ReturnsAsIs()
    {
        Environment.SetEnvironmentVariable("DOCKER_HOST", "/run/user/1000/docker.sock");
        try
        {
            Assert.Equal("/run/user/1000/docker.sock", DockerSocketPathResolver.Resolve());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", null);
        }
    }

    [Fact]
    public void Resolve_XdgRuntimeDirSetAndSocketExists_ReturnsXdgSocketPath()
    {
        string tempDir = Directory.CreateTempSubdirectory("eshop-docker-socket-test-").FullName;
        try
        {
            string socketPath = Path.Combine(tempDir, "docker.sock");
            File.WriteAllText(socketPath, string.Empty);
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", tempDir);
            try
            {
                Assert.Equal(socketPath, DockerSocketPathResolver.Resolve());
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", null);
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_XdgRuntimeDirSetButSocketMissing_FallsBackToDefaultPath()
    {
        string tempDir = Directory.CreateTempSubdirectory("eshop-docker-socket-test-").FullName;
        try
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", tempDir);
            try
            {
                Assert.Equal("/var/run/docker.sock", DockerSocketPathResolver.Resolve());
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", null);
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
