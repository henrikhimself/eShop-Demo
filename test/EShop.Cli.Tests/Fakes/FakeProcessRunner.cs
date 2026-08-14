// <copyright file="FakeProcessRunner.cs" company="Henrik Jensen">
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

// Records every invocation and returns a canned result - no real process, no Docker,
// no network. The one seam every other fake/test in this project builds on.
internal sealed class FakeProcessRunner(Func<ProcessRequest, ProcessResult>? responder = null) : IProcessRunner
{
    private readonly List<ProcessRequest> _invocations = [];

    public IReadOnlyList<ProcessRequest> Invocations => _invocations;

    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        _invocations.Add(request);
        ProcessResult result = responder is null
            ? new ProcessResult(0, string.Empty, string.Empty)
            : responder(request);

        return Task.FromResult(result);
    }
}
