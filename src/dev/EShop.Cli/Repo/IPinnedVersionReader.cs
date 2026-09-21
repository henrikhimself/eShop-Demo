// <copyright file="IPinnedVersionReader.cs" company="Henrik Jensen">
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

// Reads the repo's pinned-version files so no command hardcodes a second copy
// (AGENTS.md's "code is the authority" rule). No SDK method here - PrerequisiteChecker
// relies on global.json's own rollForward resolution instead.
internal interface IPinnedVersionReader
{
    Task<string> GetPinnedNodeVersionAsync(CancellationToken cancellationToken);

    Task<string> GetPinnedPnpmVersionAsync(CancellationToken cancellationToken);
}
