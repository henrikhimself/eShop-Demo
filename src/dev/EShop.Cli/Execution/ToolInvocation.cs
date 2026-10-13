// <copyright file="ToolInvocation.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Execution;

// What every command asks IToolExecutor to run - "dotnet", "pnpm", "aspire", "rumdl",
// "shellcheck", "plantuml", etc. ForceMode is set only by `test e2e`, which always
// needs the container's Playwright/Chromium install regardless of --tools.
// EnvironmentVariables and OutputLineHandler are set by ToolExecutor itself (not by
// callers) to pass NO_COLOR=1 through to the child process in AI mode and stream debug
// output - see ToolExecutor.RunAsync.
internal sealed record ToolInvocation(
    string Tool,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    bool Interactive = false,
    ExecutionMode? ForceMode = null,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
    Action<ProcessOutputLine>? OutputLineHandler = null);
