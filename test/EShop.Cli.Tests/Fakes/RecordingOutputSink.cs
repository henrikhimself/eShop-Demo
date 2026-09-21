// <copyright file="RecordingOutputSink.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Execution;

namespace Hj.EShop.Cli.Tests.Fakes;

// Records every call for assertion, so a command's own logic (which severities/steps
// it reports, in what order) can be tested without depending on either real sink's
// rendered text.
internal sealed class RecordingOutputSink : IOutputSink
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    public void Heading(int level, string text)
    {
        _calls.Add($"Heading({level}, \"{text}\")");
    }

    public void Text(string text)
    {
        _calls.Add($"Text(\"{text}\")");
    }

    public void Code(string languageId, string content)
    {
        _calls.Add($"Code(\"{languageId}\", \"{content}\")");
    }

    public void Table(string title, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        _calls.Add($"Table(\"{title}\", {rows.Count} rows)");
    }

    public void Status(Severity severity, string message)
    {
        _calls.Add($"Status({severity}, \"{message}\")");
    }

    public void Debug(string utility, ProcessOutputLine outputLine)
    {
        _calls.Add($"Debug(\"{utility}\", {outputLine.Stream}, \"{outputLine.Text}\")");
    }

    public IDisposable BeginStep(string name)
    {
        _calls.Add($"BeginStep(\"{name}\")");
        return NullDisposable.Instance;
    }

    public IOutputProgress BeginProgress(string description, long? total)
    {
        _calls.Add($"BeginProgress(\"{description}\", {total})");
        return new RecordingProgress();
    }

    public Task FlushPendingStepsAsync()
    {
        _calls.Add("FlushPendingStepsAsync()");
        return Task.CompletedTask;
    }

    private sealed class RecordingProgress : IOutputProgress
    {
        public void Report(long? completed, string? message)
        {
        }

        public void Dispose()
        {
        }
    }
}
