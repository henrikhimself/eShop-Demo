// <copyright file="UtilityImageSourceHasher.cs" company="Henrik Jensen">
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

using System.Security.Cryptography;
using Hj.EShop.Cli.Repo;

namespace Hj.EShop.Cli.Execution;

internal sealed class UtilityImageSourceHasher(RepoPaths paths) : IUtilityImageSourceHasher
{
    public async Task<string> ComputeAsync(CancellationToken cancellationToken)
    {
        string[] hashInputFiles =
        [
            paths.ContainerfilePath,
            paths.GlobalJsonPath,
            paths.NvmrcPath,
            paths.PackageJsonPath,
            paths.DirectoryPackagesPropsPath,
        ];

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in hashInputFiles)
        {
            hash.AppendData(await File.ReadAllBytesAsync(file, cancellationToken));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
