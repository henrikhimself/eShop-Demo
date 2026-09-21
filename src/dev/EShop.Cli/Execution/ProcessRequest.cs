// <copyright file="ProcessRequest.cs" company="Henrik Jensen">
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

// The one real OS-process boundary every tool invocation goes through (see
// IProcessRunner). Interactive is true only for the `aspire start`/wait/stop session -
// it disables output capture so the child inherits the real console directly. When
// output is captured, OutputLineHandler receives each line as it arrives.
internal sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
    bool Interactive = false,
    Action<ProcessOutputLine>? OutputLineHandler = null);
