// <copyright file="CompilerOutputParser.cs" company="Henrik Jensen">
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

// Extracts "file(line,col): error|warning ..." lines - the shape `dotnet build` and
// `tsc --noEmit` both emit.
internal static partial class CompilerOutputParser
{
    public static IReadOnlyList<Diagnostic> ParseDotnetBuildOutput(string output)
    {
        return Lines(output)
            .Where(line => DiagnosticLine().IsMatch(line) && !IsUnderObjDirectory(line))
            .Select(line => new Diagnostic(line))
            .ToArray();
    }

    public static IReadOnlyList<Diagnostic> ParseTypeScriptCompilerOutput(string output)
    {
        return Lines(output)
            .Where(line => DiagnosticLine().IsMatch(line))
            .Select(line => new Diagnostic(line))
            .ToArray();
    }

    [GeneratedRegex(@"^[^:]+\([0-9]+,[0-9]+\):\s*(error|warning)")]
    private static partial Regex DiagnosticLine();

    [GeneratedRegex("\x1b\\[[0-9;]*[a-zA-Z]")]
    private static partial Regex AnsiEscapeSequence();

    private static readonly char[] _pathSeparators = ['/', '\\'];

    private static bool IsUnderObjDirectory(string line)
    {
        int parenIndex = line.IndexOf('(', StringComparison.Ordinal);
        string path = parenIndex < 0 ? line : line[..parenIndex];
        return path.Split(_pathSeparators).Contains("obj");
    }

    private static string[] Lines(string output)
    {
        return AnsiEscapeSequence().Replace(output, string.Empty).ReplaceLineEndings("\n").Split('\n');
    }
}
