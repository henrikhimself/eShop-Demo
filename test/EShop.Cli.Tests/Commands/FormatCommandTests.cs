// <copyright file="FormatCommandTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Commands;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands;

public sealed class FormatCommandTests
{
    [Fact]
    public async Task ExecuteAsync_RunsEveryFixerAndTheSchemaRegen_ReturnsZero()
    {
        FakeToolExecutor toolExecutor = new();
        FakeApiSchemaGenerator generator = new();
        FormatCommand command = new(new RecordingOutputSink(), toolExecutor, generator, new RepoPaths("/repo"));

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(toolExecutor.Invocations, i => i.Tool == "dotnet" && i.Arguments.Contains("whitespace"));
        Assert.Contains(toolExecutor.Invocations, i => i.Tool == "dotnet" && i.Arguments.Contains("style"));
        Assert.Contains(toolExecutor.Invocations, i => i.Tool == "dotnet" && i.Arguments.Contains("analyzers"));
        Assert.Contains(toolExecutor.Invocations, i => i.Tool == "rumdl" && i.Arguments.Contains("fmt"));
        Assert.Contains(toolExecutor.Invocations, i => i.Tool == "pnpm" && i.Arguments.Contains("--fix"));
        Assert.Equal(1, generator.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_RunsIndependentBranchesConcurrently_NotSequentially()
    {
        FakeToolExecutor toolExecutor = new(
            delaySelector: invocation => invocation.Tool == "rumdl" || (invocation.Tool == "dotnet" && invocation.Arguments.Contains("whitespace"))
                ? TimeSpan.FromMilliseconds(200)
                : TimeSpan.Zero);
        FormatCommand command = new(new RecordingOutputSink(), toolExecutor, new FakeApiSchemaGenerator(), new RepoPaths("/repo"));

        var stopwatch = Stopwatch.StartNew();
        await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // The dotnet-format chain and rumdl each wait 200ms - sequential would take
        // ~400ms+; concurrent stays close to 200ms.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(350), $"Expected concurrent execution, took {stopwatch.Elapsed}.");
    }
}
