// <copyright file="RepoRootLocator.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Repo;

// Finds the repo root by looking for EShop.slnx near the published binary first
// (the normal scripts/eshop.sh case), then falling back to the current
// working directory for `dotnet run`, where AppContext.BaseDirectory is
// under bin/Debug/....
internal sealed class RepoRootLocator : IRepoRootLocator
{
    private const string SolutionFileName = "EShop.slnx";

    public string Find()
    {
        return TryFindFrom(AppContext.BaseDirectory)
            ?? TryFindFrom(Environment.CurrentDirectory)
            ?? throw new InvalidOperationException(
                $"Could not locate the repo root (a directory containing '{SolutionFileName}') " +
                $"from '{AppContext.BaseDirectory}' or '{Environment.CurrentDirectory}'.");
    }

    private static string? TryFindFrom(string startDirectory)
    {
        DirectoryInfo? current = new(startDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
