// <copyright file="HumanOutputSink.cs" company="Henrik Jensen">
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

using Spectre.Console;

namespace Hj.EShop.Cli.Output;

// Rich terminal output via Spectre.Console widgets - colors, emoji, tables, panels.
// BeginStep shows a live spinner (see SpinnerSession below); BeginProgress is still
// just a status line - nothing calls it yet, so it stays simple until something does.
internal sealed class HumanOutputSink(TimeSpan? sessionEndGracePeriod = null) : IOutputSink
{
    private readonly Lock _gate = new();
    private readonly TimeSpan _sessionEndGracePeriod = sessionEndGracePeriod ?? TimeSpan.FromMilliseconds(200);
    private SpinnerSession? _session;

    public void Heading(int level, string text)
    {
        if (level <= 1)
        {
            AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(text)}[/]").LeftJustified());
        }
        else
        {
            AnsiConsole.MarkupLine($"[bold underline]{Markup.Escape(text)}[/]");
        }
    }

    public void Text(string text)
    {
        AnsiConsole.WriteLine(text);
    }

    public void Code(string languageId, string content)
    {
        // Plain text has no fixed width to pad to, so raw ANSI bytes in captured subprocess
        // output pass through correctly instead of corrupting anything.
        AnsiConsole.WriteLine(content);
    }

    public void Table(string title, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        Spectre.Console.Table table = new Spectre.Console.Table().Title(title);
        foreach (string column in columns)
        {
            table.AddColumn(Markup.Escape(column));
        }

        foreach (IReadOnlyList<string> row in rows)
        {
            table.AddRow(row.Select(Markup.Escape).ToArray());
        }

        AnsiConsole.Write(table);
    }

    public void Status(Severity severity, string message)
    {
        (string emoji, string color) = severity switch
        {
            Severity.Success => ("✅", "green"),
            Severity.Warning => ("⚠️", "yellow"),
            Severity.Failure => ("❌", "red"),
            Severity.Info => ("ℹ️", "grey"),
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, message: null),
        };

        AnsiConsole.MarkupLine($"{emoji} [{color}]{Markup.Escape(message)}[/]");
    }

    // Steps from several commands may be in flight at once, so they all share one
    // live Progress region, lazily started on the first step and torn down once the
    // last one finishes. The next BeginStep call after that starts a fresh region.
    public IDisposable BeginStep(string name)
    {
        SpinnerSession session;
        lock (_gate)
        {
            session = _session ??= new SpinnerSession(EndSession, _sessionEndGracePeriod);
        }

        return session.AddStep(name);
    }

    public IOutputProgress BeginProgress(string description, long? total)
    {
        AnsiConsole.MarkupLine($"[dim]▸ {Markup.Escape(description)}...[/]");
        return new HumanOutputProgress();
    }

    // Bounded to 2s so a pathological Spectre issue can't hang the whole process at exit.
    public async Task FlushPendingStepsAsync()
    {
        SpinnerSession? session;
        lock (_gate)
        {
            session = _session;
        }

        if (session is null)
        {
            return;
        }

        await Task.WhenAny(session.EndAndWaitAsync(), Task.Delay(TimeSpan.FromSeconds(2)));
    }

    private void EndSession(SpinnerSession session)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_session, session))
            {
                _session = null;
            }
        }
    }

    private sealed class HumanOutputProgress : IOutputProgress
    {
        public void Report(long? completed, string? message)
        {
        }

        public void Dispose()
        {
        }
    }

    // Bridges Spectre's callback-scoped Progress.StartAsync to ad-hoc, synchronous
    // BeginStep calls: the live region stays open only while at least one step is
    // active, tracked via a simple ref count. AddStep never blocks the calling thread -
    // it only queues a continuation - so it can't starve the thread pool.
    private sealed class SpinnerSession
    {
        private readonly Lock _gate = new();
        private readonly TimeSpan _endGracePeriod;
        private readonly TaskCompletionSource _stop = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<ProgressContext> _contextReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _run;
        private int _activeSteps;

        public SpinnerSession(Action<SpinnerSession> onEnded, TimeSpan endGracePeriod)
        {
            _endGracePeriod = endGracePeriod;
            _run = RunAsync(onEnded);
        }

        public IDisposable AddStep(string name)
        {
            lock (_gate)
            {
                _activeSteps++;
            }

            StepHandle handle = new(this);
            _contextReady.Task.ContinueWith(
                contextTask =>
                {
                    ProgressTask task;
                    lock (_gate)
                    {
                        task = contextTask.Result.AddTask(Markup.Escape(name));
                        task.IsIndeterminate = true;
                    }

                    handle.Attach(task);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return handle;
        }

        private async Task RunAsync(Action<SpinnerSession> onEnded)
        {
            await AnsiConsole.Progress()
                .Columns(new SpinnerColumn(), new TaskDescriptionColumn())
                .StartAsync(async context =>
                {
                    _contextReady.SetResult(context);
                    await _stop.Task;
                });

            onEnded(this);
        }

        // Forces the session to end right now, however far into (or before) its
        // grace period it currently is, and waits for the live region to actually
        // finish tearing down.
        public async Task EndAndWaitAsync()
        {
            _stop.TrySetResult();
            await _run;
        }

        // Only called once the step's task has actually been added - so the session
        // (and its live region) can never end before every added task is real.
        private void CompleteStep()
        {
            bool allDone;
            lock (_gate)
            {
                allDone = --_activeSteps == 0;
            }

            if (allDone)
            {
                _ = ScheduleEndAsync();
            }
        }

        // Debounced, not immediate - ending/restarting the live region on every single
        // BeginStep call meant no step ever animated (consecutive calls are typically
        // microseconds apart). A step starting within the grace period cancels this via
        // the ref-count check below.
        private async Task ScheduleEndAsync()
        {
            await Task.Delay(_endGracePeriod);
            lock (_gate)
            {
                if (_activeSteps != 0)
                {
                    return;
                }
            }

            _stop.TrySetResult();
        }

        private sealed class StepHandle(SpinnerSession session) : IDisposable
        {
            private readonly Lock _gate = new();
            private ProgressTask? _task;
            private bool _disposed;

            // Runs on whatever thread the session's context-ready continuation lands
            // on - may race with a Dispose() that already happened before the task
            // even existed.
            public void Attach(ProgressTask task)
            {
                bool alreadyDisposed;
                lock (_gate)
                {
                    alreadyDisposed = _disposed;
                    _task = task;
                }

                if (alreadyDisposed)
                {
                    lock (session._gate)
                    {
                        task.StopTask();
                    }

                    session.CompleteStep();
                }
            }

            public void Dispose()
            {
                ProgressTask? task;
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                    task = _task;
                }

                if (task is null)
                {
                    // Attach hasn't run yet - it'll see _disposed and finish this itself.
                    return;
                }

                lock (session._gate)
                {
                    task.StopTask();
                }

                session.CompleteStep();
            }
        }
    }
}
