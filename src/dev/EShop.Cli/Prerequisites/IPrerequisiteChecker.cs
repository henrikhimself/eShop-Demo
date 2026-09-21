// <copyright file="IPrerequisiteChecker.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Prerequisites;

// Checks the developer's *local* dotnet/node/pnpm installs against this repo's own
// pins - only relevant under --tools auto/local, where a mismatched local tool would
// silently be used instead of the container's vetted one.
internal interface IPrerequisiteChecker
{
    Task<PrerequisiteCheckResult> CheckAsync(CancellationToken cancellationToken);
}
