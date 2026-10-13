// <copyright file="AiOutputSinkTests.cs" company="Henrik Jensen">
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
using Xunit;

namespace Hj.EShop.Cli.Tests.Output;

// AI-mode text is a stable contract other tooling (and agents) may parse - these tests
// lock down the exact wording/shape, not just "something was written".
public sealed class AiOutputSinkTests
{
    [Fact]
    public void Heading_WritesMarkdownAtxHeading()
    {
        StringWriter writer = new();
        AiOutputSink sink = new(writer);

        sink.Heading(2, "Build Result");

        Assert.Equal("## Build Result\r\n\r\n".ReplaceLineEndings(), writer.ToString().ReplaceLineEndings());
    }

    [Fact]
    public void Heading_ClampsLevelToValidRange()
    {
        StringWriter writer = new();
        AiOutputSink sink = new(writer);

        sink.Heading(99, "Too Deep");

        Assert.StartsWith("######", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Code_WritesFencedCodeBlock()
    {
        StringWriter writer = new();
        AiOutputSink sink = new(writer);

        sink.Code("plain", "line one");

        string expected = "```plain\r\nline one\r\n```\r\n\r\n".ReplaceLineEndings();
        Assert.Equal(expected, writer.ToString().ReplaceLineEndings());
    }

    [Fact]
    public void Status_NonInfoSeverity_WritesExplicitStatusAndSummaryLines()
    {
        StringWriter writer = new();
        AiOutputSink sink = new(writer);

        sink.Status(Severity.Failure, "Build finished with 3 errors.");

        string expected = "Status: failure\r\nSummary: Build finished with 3 errors.\r\n\r\n".ReplaceLineEndings();
        Assert.Equal(expected, writer.ToString().ReplaceLineEndings());
    }

    [Fact]
    public void Status_InfoSeverity_WritesInfoLineOnly()
    {
        StringWriter writer = new();
        AiOutputSink sink = new(writer);

        sink.Status(Severity.Info, "Restoring packages.");

        string expected = "Info: Restoring packages.\r\n\r\n".ReplaceLineEndings();
        Assert.Equal(expected, writer.ToString().ReplaceLineEndings());
    }

    [Fact]
    public void BeginStep_WritesStableStepLine_AndDisposeAddsNothing()
    {
        StringWriter writer = new();
        AiOutputSink sink = new(writer);

        using (sink.BeginStep("dotnet build EShop.slnx"))
        {
            // Intentionally empty - the point of this test is that closing the step
            // adds no further output in AI mode.
        }

        string expected = "Step: dotnet build EShop.slnx\r\n".ReplaceLineEndings();
        Assert.Equal(expected, writer.ToString().ReplaceLineEndings());
    }

    [Fact]
    public void BeginStep_WhileOpen_WritesLivenessHeartbeatOnTheConfiguredInterval()
    {
        StringWriter writer = new();
        ControllableTimeProvider timeProvider = new();
        AiOutputSink sink = new(writer, TimeSpan.FromMinutes(1), timeProvider);

        using (sink.BeginStep("dotnet build EShop.slnx"))
        {
            timeProvider.FireTimers();
        }

        Assert.Contains("still running", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void BeginStep_AfterDispose_WritesNoFurtherHeartbeats()
    {
        StringWriter writer = new();
        ControllableTimeProvider timeProvider = new();
        AiOutputSink sink = new(writer, TimeSpan.FromMinutes(1), timeProvider);

        IDisposable step = sink.BeginStep("dotnet build EShop.slnx");
        timeProvider.FireTimers();
        step.Dispose();
        string afterDispose = writer.ToString();

        timeProvider.FireTimers();

        Assert.Equal(afterDispose, writer.ToString());
    }

    [Fact]
    public void BeginProgress_ReportDoesNotWriteAnything()
    {
        StringWriter writer = new();
        AiOutputSink sink = new(writer);

        using IOutputProgress progress = sink.BeginProgress("Publishing", 100);
        progress.Report(50, "halfway");

        string expected = "Step: Publishing\r\n".ReplaceLineEndings();
        Assert.Equal(expected, writer.ToString().ReplaceLineEndings());
    }

    private sealed class ControllableTimeProvider : TimeProvider
    {
        private readonly List<ControllableTimer> _timers = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ControllableTimer timer = new(callback, state);
            _timers.Add(timer);
            return timer;
        }

        public void FireTimers()
        {
            foreach (ControllableTimer timer in _timers)
            {
                timer.Fire();
            }
        }

        private sealed class ControllableTimer(TimerCallback callback, object? state) : ITimer
        {
            private bool _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                return !_disposed;
            }

            public void Dispose()
            {
                _disposed = true;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void Fire()
            {
                if (!_disposed)
                {
                    callback(state);
                }
            }
        }
    }

    [Fact]
    public void Table_WritesMarkdownPipeTable()
    {
        StringWriter writer = new();
        AiOutputSink sink = new(writer);

        sink.Table("Diagnostics", ["File", "Message"], [["Foo.cs", "error"]]);

        string expected =
            "### Diagnostics\r\n\r\n" +
            "| File | Message |\r\n" +
            "| --- | --- |\r\n" +
            "| Foo.cs | error |\r\n\r\n";
        Assert.Equal(expected.ReplaceLineEndings(), writer.ToString().ReplaceLineEndings());
    }
}
