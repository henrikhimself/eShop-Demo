// <copyright file="UtilityImageSourceHasherTests.cs" company="Henrik Jensen">
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
using Xunit;

namespace Hj.EShop.Cli.Tests.Execution;

public sealed class UtilityImageSourceHasherTests
{
    [Fact]
    public async Task ComputeAsync_SameFileContents_ReturnsSameHexHashBothTimes()
    {
        using var repo = TempRepo.Create();
        UtilityImageSourceHasher hasher = new(repo.Paths);

        string first = await hasher.ComputeAsync(TestContext.Current.CancellationToken);
        string second = await hasher.ComputeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public async Task ComputeAsync_ContainerfileContentChanges_ReturnsDifferentHash()
    {
        using var repo = TempRepo.Create();
        UtilityImageSourceHasher hasher = new(repo.Paths);
        string before = await hasher.ComputeAsync(TestContext.Current.CancellationToken);

        await File.AppendAllTextAsync(repo.Paths.ContainerfilePath, "\n# changed\n", TestContext.Current.CancellationToken);

        string after = await hasher.ComputeAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task ComputeAsync_VersionPinFileChanges_ReturnsDifferentHash()
    {
        using var repo = TempRepo.Create();
        UtilityImageSourceHasher hasher = new(repo.Paths);
        string before = await hasher.ComputeAsync(TestContext.Current.CancellationToken);

        await File.WriteAllTextAsync(repo.Paths.NvmrcPath, "99.0.0\n", TestContext.Current.CancellationToken);

        string after = await hasher.ComputeAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(before, after);
    }

    // Creates the minimal on-disk layout UtilityImageSourceHasher reads from - a real
    // temp directory, not RepoPaths("/repo"), since the hasher must actually read
    // these files' bytes rather than just be told their path.
    private sealed class TempRepo : IDisposable
    {
        private readonly string _root;

        private TempRepo(string root)
        {
            _root = root;
            Paths = new RepoPaths(root);
        }

        public RepoPaths Paths { get; }

        public static TempRepo Create()
        {
            string root = Directory.CreateTempSubdirectory("eshop-utility-image-source-hasher-").FullName;
            TempRepo repo = new(root);

            Directory.CreateDirectory(Path.GetDirectoryName(repo.Paths.ContainerfilePath)!);
            File.WriteAllText(repo.Paths.ContainerfilePath, "FROM mcr.microsoft.com/dotnet/sdk:10.0\n");
            File.WriteAllText(repo.Paths.GlobalJsonPath, "{\"sdk\":{\"version\":\"10.0.100\"}}\n");
            File.WriteAllText(repo.Paths.NvmrcPath, "24.0.0\n");
            File.WriteAllText(repo.Paths.PackageJsonPath, "{\"packageManager\":\"pnpm@9.1.0\"}\n");
            File.WriteAllText(repo.Paths.DirectoryPackagesPropsPath, "<Project></Project>\n");

            return repo;
        }

        public void Dispose()
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
