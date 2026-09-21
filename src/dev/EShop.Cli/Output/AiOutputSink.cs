// <copyright file="AiOutputSink.cs" company="Henrik Jensen">
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

using System.Diagnostics;

namespace Hj.EShop.Cli.Output;

// Plain, ANSI-free Markdown-ish text: no emoji, no spinners, no progress ticks, no
// color. Wording is fixed per call site (never ad hoc interpolated beyond the values
// callers pass in) so it stays stable release to release - see
// doc/adr/0016-eshop-cli-replaces-bash-scripts.md.
//
// BeginStep's step can run for minutes (a full dotnet build, a test run, ...) with no
// other output in between - an AI coding agent watching the process can misread that
// silence as a hang and kill it. heartbeatInterval (default 1 minute; overridable
// only by tests) makes BeginStep write a liveness line on that cadence for as long as
// the step is open.
internal sealed class AiOutputSink(TextWriter writer, TimeSpan? heartbeatInterval = null) : IOutputSink
{
    private readonly TimeSpan _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromMinutes(1);

    public void Heading(int level, string text)
    {
        int clampedLevel = Math.Clamp(level, 1, 6);
        writer.WriteLine($"{new string('#', clampedLevel)} {text}");
        writer.WriteLine();
    }

    public void Text(string text)
    {
        writer.WriteLine(text);
        writer.WriteLine();
    }

    public void Code(string languageId, string content)
    {
        writer.WriteLine($"```{languageId}");
        writer.WriteLine(content);
        writer.WriteLine("```");
        writer.WriteLine();
    }

    public void Table(string title, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        writer.WriteLine($"### {title}");
        writer.WriteLine();
        writer.WriteLine($"| {string.Join(" | ", columns)} |");
        writer.WriteLine($"| {string.Join(" | ", columns.Select(_ => "---"))} |");
        foreach (IReadOnlyList<string> row in rows)
        {
            writer.WriteLine($"| {string.Join(" | ", row)} |");
        }

        writer.WriteLine();
    }

    public void Status(Severity severity, string message)
    {
        if (severity is Severity.Info)
        {
            writer.WriteLine($"Info: {message}");
        }
        else
        {
            writer.WriteLine($"Status: {severity.ToString().ToLowerInvariant()}");
            writer.WriteLine($"Summary: {message}");
        }

        writer.WriteLine();
    }

    public IDisposable BeginStep(string name)
    {
        writer.WriteLine($"Step: {name}");
        return new HeartbeatHandle(writer, name, _heartbeatInterval);
    }

    public IOutputProgress BeginProgress(string description, long? total)
    {
        writer.WriteLine($"Step: {description}");
        return new AiOutputProgress();
    }

    // Nothing to settle here - unlike HumanOutputSink's spinner, a step's Dispose()
    // finishes immediately.
    public Task FlushPendingStepsAsync()
    {
        return Task.CompletedTask;
    }

    // AI mode's whole point is no per-tick churn - Report() is deliberately silent.
    private sealed class AiOutputProgress : IOutputProgress
    {
        public void Report(long? completed, string? message)
        {
        }

        public void Dispose()
        {
        }
    }

    // Console.Out is already BCL-synchronized, so concurrent steps' timers writing
    // independently is safe without extra locking here.
    private sealed class HeartbeatHandle : IDisposable
    {
        private readonly TextWriter _writer;
        private readonly string _name;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly Timer _timer;

        public HeartbeatHandle(TextWriter writer, string name, TimeSpan interval)
        {
            _writer = writer;
            _name = name;
            _timer = new Timer(_ => WriteHeartbeat(), null, interval, interval);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private void WriteHeartbeat()
        {
            _writer.WriteLine($"Step: {_name} (still running after {(int)_stopwatch.Elapsed.TotalMinutes} min)");
        }
    }
}
