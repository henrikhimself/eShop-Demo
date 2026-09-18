// <copyright file="HumanOutputSinkTests.cs" company="Henrik Jensen">
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
using System.Text;
using Hj.EShop.Cli.Output;
using Spectre.Console;
using Xunit;

namespace Hj.EShop.Cli.Tests.Output;

// Thin smoke tests only - no Spectre.Console.Testing package is cleared for use yet
// (see doc/adr/0013-pinned-dependency-versions-and-7-day-quarantine.md), so these
// can't assert on rendered spinner frames. They lock down the concurrency/lifecycle
// contract instead: BeginStep is safe to call from several threads at once, a session
// that's fully wound down lets the next BeginStep call start cleanly, and
// FlushPendingStepsAsync can force that winding-down instead of waiting it out.
public sealed class HumanOutputSinkTests
{
    [Fact]
    public void BeginStep_ReturnsADisposableThatDisposesWithoutThrowing()
    {
        HumanOutputSink sink = new();

        IDisposable step = sink.BeginStep("dotnet build");

        Assert.NotNull(step);
        step.Dispose();
    }

    [Fact]
    public async Task BeginStep_CalledConcurrentlyFromSeveralThreads_DoesNotThrowOrHang()
    {
        HumanOutputSink sink = new();
        int completed = 0;

        Task[] steps =
        [
            .. Enumerable.Range(0, 8).Select(i => Task.Run(() =>
            {
                using IDisposable step = sink.BeginStep($"step {i}");
                Interlocked.Increment(ref completed);
            })),
        ];

        await Task.WhenAll(steps).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(8, completed);
    }

    [Fact]
    public async Task BeginStep_CalledAgainWithinTheGracePeriod_DoesNotThrowOrHang()
    {
        // Exercises the "reuse the still-open session instead of tearing it down and
        // restarting" path a quick sequence of steps within one command takes.
        HumanOutputSink sink = new(sessionEndGracePeriod: TimeSpan.FromMilliseconds(200));

        IDisposable first = sink.BeginStep("first");
        first.Dispose();
        await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);

        IDisposable second = sink.BeginStep("second");
        Assert.NotNull(second);
        second.Dispose();
    }

    [Fact]
    public async Task BeginStep_AfterTheGracePeriodElapsesWithNoNewStep_NextCallStartsAFreshSessionCleanly()
    {
        HumanOutputSink sink = new(sessionEndGracePeriod: TimeSpan.FromMilliseconds(20));

        IDisposable first = sink.BeginStep("first");
        first.Dispose();

        // Comfortably longer than the grace period above, so the first session has
        // actually ended by the time "second" starts.
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        IDisposable second = sink.BeginStep("second");
        Assert.NotNull(second);
        second.Dispose();
    }

    [Fact]
    public async Task FlushPendingStepsAsync_EndsAPendingSession_WithoutWaitingOutTheGracePeriod()
    {
        // A grace period long enough that "FlushPendingStepsAsync returned quickly" can only
        // mean it short-circuited the wait, not that the wait was naturally short.
        HumanOutputSink sink = new(sessionEndGracePeriod: TimeSpan.FromSeconds(30));
        IDisposable step = sink.BeginStep("first");
        step.Dispose();

        var stopwatch = Stopwatch.StartNew();
        await sink.FlushPendingStepsAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"FlushPendingStepsAsync took {stopwatch.Elapsed} - expected it to short-circuit the grace period.");
    }

    // Code() writes plain text, not a Panel and not through markup parsing, so
    // bracketed content passes through unchanged while color remains available
    // elsewhere.
    [Fact]
    public void Code_WritesContentWithoutMarkupParsing()
    {
        const string content = "[bracketed] content";
        StringWriter writer = new();
        IAnsiConsole originalConsole = AnsiConsole.Console;
        try
        {
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.No,
                Out = new FixedWidthAnsiConsoleOutput(writer, width: 80),
            });

            // Would throw (unmatched '[') or drop the brackets if this parsed as
            // Spectre markup instead of writing plain text.
            new HumanOutputSink().Code("plain", content);
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }

        Assert.Equal(content, writer.ToString().TrimEnd());
    }

    private sealed class FixedWidthAnsiConsoleOutput(TextWriter writer, int width) : IAnsiConsoleOutput
    {
        public TextWriter Writer => writer;

        public bool IsTerminal => false;

        public int Width => width;

        public int Height => 50;

        public void SetEncoding(Encoding encoding)
        {
        }
    }
}
