// <copyright file="EnvCommand.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Repo;
using Spectre.Console.Cli;

namespace Hj.EShop.Cli.Commands;

// Prints the same .cache/-sandboxed environment variables ToolExecutor.RunLocalAsync
// sets for a locally-run dev tool (see LocalToolEnvironment) as `export` statements a
// developer's own shell can eval - for running a utility the CLI does not wrap itself
// against the same sandbox (e.g. so it never touches the developer's real $HOME).
//
// Only ever calls IOutputSink.Text with plain "export KEY='VALUE'"/"# ..." lines -
// never Heading/Table/Status/Code, whose Human-mode rendering (rule, markup, emoji) is
// not valid shell - so the output stays safe to eval under both output modes.
//
// Never `source scripts/eshop.sh env` directly: that wrapper script `cd`s to the repo
// root and ends with `exec`, which would replace the calling shell process instead of
// returning to it. Use `eval "$(./scripts/eshop.sh env)"` instead - it runs the script
// in a subshell via command substitution and only evals the printed export lines into
// the caller's own shell.
internal sealed class EnvCommand(IOutputSink output, RepoPaths paths) : AsyncCommand<DefaultSettings>
{
    protected override Task<int> ExecuteAsync(CommandContext context, DefaultSettings settings, CancellationToken cancellationToken)
    {
        output.Text("# eval \"$(./scripts/eshop.sh env)\" - sets the .cache/ sandbox eshop.sh uses for local tools.");
        foreach ((string key, string value) in LocalToolEnvironment.Build(paths))
        {
            output.Text($"export {key}={ShellQuote(value)}");
        }

        return Task.FromResult(0);
    }

    // Single-quoted, with any embedded single quote closed/escaped/reopened - the
    // standard POSIX-safe quoting form. Values here are always repo-relative
    // filesystem paths or fixed literals, never developer input.
    private static string ShellQuote(string value)
    {
        return $"'{value.Replace("'", "'\\''")}'";
    }
}
