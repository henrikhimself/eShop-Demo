// <copyright file="EnvCommandTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Commands;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests.Commands;

public sealed class EnvCommandTests
{
    [Fact]
    public async Task ExecuteAsync_PrintsOneExportLinePerLocalToolEnvironmentVariable()
    {
        RepoPaths paths = new("/repo");
        RecordingOutputSink output = new();
        EnvCommand command = new(output, paths);

        int exitCode = await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);

        // Every variable ToolExecutor.RunLocalAsync sets for a locally-run tool must
        // also be printed here, in the same order, or the two would silently drift.
        foreach ((string key, string value) in LocalToolEnvironment.Build(paths))
        {
            Assert.Contains($"Text(\"export {key}='{value}'\")", output.Calls);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NeverCallsFormattingOnlySafeAsPlainText()
    {
        // Heading/Table/Status/Code render as Markdown or Spectre markup in at least
        // one output mode - neither is valid POSIX shell, so calling them here would
        // break `eval "$(./scripts/eshop.sh env)"`.
        RecordingOutputSink output = new();
        EnvCommand command = new(output, new RepoPaths("/repo"));

        await ((ICommand<DefaultSettings>)command).ExecuteAsync(
            context: null!, settings: new DefaultSettings(), TestContext.Current.CancellationToken);

        Assert.All(output.Calls, call => Assert.StartsWith("Text(", call, StringComparison.Ordinal));
    }
}
