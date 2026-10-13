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

// Emits shell-safe exports for the local-tool .cache sandbox. Use `eval "$(./scripts/eshop.sh env)"`; sourcing the wrapper replaces the caller's shell.
internal sealed class EnvCommand(IOutputSink output, ILocalToolLocator localToolLocator, RepoPaths paths) : AsyncCommand<DefaultSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, DefaultSettings settings, CancellationToken cancellationToken)
    {
        output.Text("# eval \"$(./scripts/eshop.sh env)\" - sets the .cache/ sandbox eshop.sh uses for local tools.");
        foreach ((string key, string value) in LocalToolEnvironment.Build(paths, localToolLocator))
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
