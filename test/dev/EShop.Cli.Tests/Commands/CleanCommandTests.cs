// <copyright file="CleanCommandTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.AppHost;
using Hj.EShop.Cli.Commands;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands;

public sealed class CleanCommandTests
{
    [Fact]
    public async Task ExecuteAsync_AlreadyRunning_FailsWithoutDeletingAnything()
    {
        string tempRoot = CreateTempRepo();
        try
        {
            RepoPaths paths = new(tempRoot);
            string binDir = Path.Combine(paths.Root, "src", "SomeProject", "bin");
            Directory.CreateDirectory(binDir);
            CleanCommand command = new(new RecordingOutputSink(), new FakeAppHostGuard(alreadyRunning: true), paths);

            int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
                context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

            Assert.Equal(1, exitCode);
            Assert.True(Directory.Exists(binDir));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NothingRunning_DeletesBinObjNextCachesAndTmp()
    {
        string tempRoot = CreateTempRepo();
        try
        {
            RepoPaths paths = new(tempRoot);
            string projectBin = Path.Combine(paths.Root, "src", "SomeProject", "bin");
            string projectObj = Path.Combine(paths.Root, "test", "SomeProject.Tests", "obj");
            string nextDir = Path.Combine(paths.SellerPortalWebDir, ".next");
            string nextContainerDir = Path.Combine(paths.SellerPortalWebDir, ".next-container");
            CreateFile(Path.Combine(projectBin, "SomeProject.dll"));
            CreateFile(Path.Combine(projectObj, "SomeProject.dll"));
            CreateFile(Path.Combine(nextDir, "cache", "x"));
            CreateFile(Path.Combine(nextContainerDir, "cache", "x"));
            CreateFile(Path.Combine(paths.TmpDir, "TestResults", "x.trx"));
            CleanCommand command = new(new RecordingOutputSink(), new FakeAppHostGuard(alreadyRunning: false), paths);

            int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
                context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.False(Directory.Exists(projectBin));
            Assert.False(Directory.Exists(projectObj));
            Assert.False(Directory.Exists(nextDir));
            Assert.False(Directory.Exists(nextContainerDir));
            Assert.False(Directory.Exists(paths.TmpDir));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NothingRunning_NeverTouchesCacheNodeModulesOrCommittedSchema()
    {
        string tempRoot = CreateTempRepo();
        try
        {
            RepoPaths paths = new(tempRoot);
            string nugetCache = Path.Combine(paths.Root, ".cache", "nuget-packages", "some.nupkg");
            string nodeModulesFile = Path.Combine(paths.Root, "node_modules", "some-package", "index.js");
            CreateFile(nugetCache);
            CreateFile(nodeModulesFile);
            CreateFile(paths.GeneratedApiSchema);
            CleanCommand command = new(new RecordingOutputSink(), new FakeAppHostGuard(alreadyRunning: false), paths);

            int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
                context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(nugetCache));
            Assert.True(File.Exists(nodeModulesFile));
            Assert.True(File.Exists(paths.GeneratedApiSchema));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NothingToClean_IsSafeNoOp()
    {
        string tempRoot = CreateTempRepo();
        try
        {
            RepoPaths paths = new(tempRoot);
            CleanCommand command = new(new RecordingOutputSink(), new FakeAppHostGuard(alreadyRunning: false), paths);

            int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
                context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempRepo()
    {
        return Directory.CreateTempSubdirectory("eshop-clean-command-").FullName;
    }

    private static void CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "content");
    }

    private sealed class FakeAppHostGuard(bool alreadyRunning) : IAppHostGuard
    {
        public Task<bool> IsAlreadyRunningAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(alreadyRunning);
        }
    }
}
