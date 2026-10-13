// <copyright file="Diagnostic.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Diagnostics;

// One line of the consolidated build report - the tools feeding into it (dotnet build,
// SARIF analyzers, dotnet format, rumdl, shellcheck, ESLint, tsc) each format their own
// "file(line,col): ..." text differently, so this wraps the already-formatted line
// rather than imposing one structured shape that doesn't actually fit all of them.
internal sealed record Diagnostic(string Text)
{
    public override string ToString()
    {
        return Text;
    }
}
