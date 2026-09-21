// <copyright file="GlobalOptions.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Output;

namespace Hj.EShop.Cli;

// The fully-resolved result of GlobalOptionsInterceptor's precedence chain
// (flag > env var > default) for every global option - see GlobalOptionsInterceptor.
// UnusableLocalTools defaults to empty so existing call sites that only care about
// Tools/Output don't need to pass it - ToolExecutor is the only reader.
internal sealed record GlobalOptions(ExecutionMode Tools, OutputMode Output, IReadOnlySet<string>? UnusableLocalTools = null)
{
    public IReadOnlySet<string> UnusableLocalTools { get; init; } = UnusableLocalTools ?? new HashSet<string>();

    public bool Debug { get; init; }
}
