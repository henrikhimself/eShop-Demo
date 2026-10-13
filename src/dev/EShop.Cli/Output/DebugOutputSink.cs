// <copyright file="DebugOutputSink.cs" company="Henrik Jensen">
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

using System.Globalization;
using Hj.EShop.Cli.Execution;

namespace Hj.EShop.Cli.Output;

internal sealed class DebugOutputSink(TextWriter writer, Func<DateTimeOffset>? now = null) : IOutputSink
{
    private readonly TextWriter _writer = TextWriter.Synchronized(writer);
    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.UtcNow);

    public void Heading(int level, string text)
    {
        Write($"heading-{Math.Clamp(level, 1, 6)}", text);
    }

    public void Text(string text)
    {
        Write("text", text);
    }

    public void Code(string languageId, string content)
    {
        Write($"code:{languageId}", content);
    }

    public void Table(string title, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        Write("table", $"{title}: {rows.Count} rows");
    }

    public void Status(Severity severity, string message)
    {
        Write($"status:{severity.ToString().ToLowerInvariant()}", message);
    }

    public void Debug(string utility, ProcessOutputLine outputLine)
    {
        string stream = outputLine.Stream == ProcessOutputStream.StandardOutput ? "stdout" : "stderr";
        Write($"{utility} {stream}", outputLine.Text);
    }

    public IDisposable BeginStep(string name)
    {
        Write("step", name);
        return NullDisposable.Instance;
    }

    public IOutputProgress BeginProgress(string description, long? total)
    {
        Write("progress", description);
        return NullOutputProgress.Instance;
    }

    public Task FlushPendingStepsAsync()
    {
        return Task.CompletedTask;
    }

    private void Write(string category, string message)
    {
        foreach (string line in message.ReplaceLineEndings("\n").Split('\n'))
        {
            string timestamp = _now().ToString("O", CultureInfo.InvariantCulture);
            _writer.WriteLine($"{timestamp} [{category}] {line}");
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class NullOutputProgress : IOutputProgress
    {
        public static readonly NullOutputProgress Instance = new();

        public void Report(long? completed, string? message)
        {
        }

        public void Dispose()
        {
        }
    }
}
