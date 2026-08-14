// <copyright file="FakeToolExecutor.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Tests.Fakes;

// Bypasses Auto/Local/Container resolution entirely - lets command-level tests (e.g.
// BuildCommand) control each tool's canned result by name without wiring up the whole
// execution-strategy layer. Optional delaySelector lets a test prove two branches
// actually run concurrently (total time tracks the longer delay, not the sum) rather
// than just looking concurrent in the source.
internal sealed class FakeToolExecutor(Func<ToolInvocation, ProcessResult>? responder = null, Func<ToolInvocation, TimeSpan>? delaySelector = null)
    : IToolExecutor
{
    private readonly List<ToolInvocation> _invocations = [];

    public IReadOnlyList<ToolInvocation> Invocations => _invocations;

    public async Task<ProcessResult> RunAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        lock (_invocations)
        {
            _invocations.Add(invocation);
        }

        TimeSpan delay = delaySelector?.Invoke(invocation) ?? TimeSpan.Zero;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        return responder is null ? new ProcessResult(0, string.Empty, string.Empty) : responder(invocation);
    }
}
