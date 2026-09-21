// <copyright file="GccStyleOutputParser.cs" company="Henrik Jensen">
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

using System.Text.RegularExpressions;

namespace Hj.EShop.Cli.Diagnostics;

// Extracts "file:line:col: ..." lines - the shape rumdl, `shellcheck --format=gcc`,
// and ESLint's `--format unix` all emit.
internal static partial class GccStyleOutputParser
{
    public static IReadOnlyList<Diagnostic> Parse(string output)
    {
        return output.ReplaceLineEndings("\n").Split('\n')
            .Where(line => DiagnosticLine().IsMatch(line))
            .Select(line => new Diagnostic(line))
            .ToArray();
    }

    [GeneratedRegex(@"^[^:]+:[0-9]+:[0-9]+: ")]
    private static partial Regex DiagnosticLine();
}
