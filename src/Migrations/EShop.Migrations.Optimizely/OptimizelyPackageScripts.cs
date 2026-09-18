// <copyright file="OptimizelyPackageScripts.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Migrations.Optimizely;

// IncrementalScripts is sorted ascending by version (see OptimizelyPackageScriptLocator).
// See doc/CHRONICLE.md — BaselineScript is idempotent (no-ops via AlreadyIn), so it's always safe to include first.
public sealed record OptimizelyPackageScripts(FileInfo? BaselineScript, IReadOnlyList<FileInfo> IncrementalScripts);
