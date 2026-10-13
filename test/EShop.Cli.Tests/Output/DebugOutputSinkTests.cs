// <copyright file="DebugOutputSinkTests.cs" company="Henrik Jensen">
// Copyright 2026 Henrik Jensen
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Output;
using Xunit;

namespace Hj.EShop.Cli.Tests.Output;

public sealed class DebugOutputSinkTests
{
    [Fact]
    public void Debug_WritesTimestampedPrefixedOutputLine()
    {
        StringWriter writer = new();
        DateTimeOffset timestamp = new(2026, 9, 21, 16, 0, 0, TimeSpan.Zero);
        DebugOutputSink sink = new(writer, () => timestamp);

        sink.Debug("pnpm", new ProcessOutputLine(ProcessOutputStream.StandardError, "network retry"));

        Assert.Equal("2026-09-21T16:00:00.0000000+00:00 [pnpm stderr] network retry", writer.ToString().Trim());
    }

    [Fact]
    public void BeginStep_WritesPlainTextAndDoesNotStartSpinner()
    {
        StringWriter writer = new();
        DebugOutputSink sink = new(writer, () => DateTimeOffset.UnixEpoch);

        IDisposable step = sink.BeginStep("pnpm install");
        step.Dispose();

        Assert.Equal("1970-01-01T00:00:00.0000000+00:00 [step] pnpm install", writer.ToString().Trim());
    }
}
