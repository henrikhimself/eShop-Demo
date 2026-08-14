// <copyright file="UtilityImageProvisionerTests.cs" company="Henrik Jensen">
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

public sealed class UtilityImageProvisionerTests
{
    [Fact]
    public async Task EnsureBuiltAsync_ImagePresentWithMatchingHashLabel_DoesNotBuild()
    {
        FakeProcessRunner processRunner = new(_ => new ProcessResult(0, "fakehash\n", string.Empty));
        UtilityImageProvisioner provisioner = new(
            processRunner, new RepoPaths("/repo"), () => new RecordingOutputSink(), new FakeUtilityImageSourceHasher("fakehash"));

        await provisioner.EnsureBuiltAsync(TestContext.Current.CancellationToken);

        Assert.Single(processRunner.Invocations);
        Assert.Equal("image", processRunner.Invocations[0].Arguments[0]);
    }

    [Fact]
    public async Task EnsureBuiltAsync_ImageMissing_BuildsFromContainerfileWithHashLabel()
    {
        FakeProcessRunner processRunner = new(request =>
            request.Arguments[0] == "image"
                ? new ProcessResult(1, string.Empty, "no such image")
                : new ProcessResult(0, string.Empty, string.Empty));
        RepoPaths paths = new("/repo");
        UtilityImageProvisioner provisioner = new(
            processRunner, paths, () => new RecordingOutputSink(), new FakeUtilityImageSourceHasher("fakehash"));

        await provisioner.EnsureBuiltAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, processRunner.Invocations.Count);
        ProcessRequest build = processRunner.Invocations[1];
        Assert.Equal(
            ["build", "--label", "eshop.source-hash=fakehash", "-t", RepoPaths.UtilityImageTag, "-f", paths.ContainerfilePath, paths.Root],
            build.Arguments);
    }

    [Fact]
    public async Task EnsureBuiltAsync_ImagePresentButHashLabelMismatched_Rebuilds()
    {
        // The image tag alone is not sufficient - a stale hash label (Containerfile or a
        // version pin changed since the image was built) must still trigger a rebuild.
        FakeProcessRunner processRunner = new(request =>
            request.Arguments[0] == "image"
                ? new ProcessResult(0, "old-hash\n", string.Empty)
                : new ProcessResult(0, string.Empty, string.Empty));
        RepoPaths paths = new("/repo");
        UtilityImageProvisioner provisioner = new(
            processRunner, paths, () => new RecordingOutputSink(), new FakeUtilityImageSourceHasher("new-hash"));

        await provisioner.EnsureBuiltAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, processRunner.Invocations.Count);
        Assert.Contains("--label", processRunner.Invocations[1].Arguments);
        Assert.Contains("eshop.source-hash=new-hash", processRunner.Invocations[1].Arguments);
    }

    [Fact]
    public async Task EnsureBuiltAsync_BuildFails_Throws()
    {
        FakeProcessRunner processRunner = new(request =>
            request.Arguments[0] == "image"
                ? new ProcessResult(1, string.Empty, "no such image")
                : new ProcessResult(1, string.Empty, "build failed"));
        UtilityImageProvisioner provisioner = new(
            processRunner, new RepoPaths("/repo"), () => new RecordingOutputSink(), new FakeUtilityImageSourceHasher());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provisioner.EnsureBuiltAsync(TestContext.Current.CancellationToken));
    }
}
