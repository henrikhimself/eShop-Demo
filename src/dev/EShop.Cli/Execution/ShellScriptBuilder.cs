// <copyright file="ShellScriptBuilder.cs" company="Henrik Jensen">
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

using System.Text;

namespace Hj.EShop.Cli.Execution;

// Composes a POSIX shell script line by line. Every line ends with `\n` regardless of
// the source file's line endings or the platform: a CRLF in a script makes dash fail
// (`set -eu\r` -> "set: Illegal option -").
internal sealed class ShellScriptBuilder
{
    private readonly StringBuilder _script = new();

    public static string Quote(string value)
    {
        return "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }

    // Accepts embedded line breaks too: each one is normalized the same way.
    public ShellScriptBuilder Append(string line)
    {
        string normalized = line.ReplaceLineEndings("\n");
        _script.Append(normalized);
        if (!normalized.EndsWith('\n'))
        {
            _script.Append('\n');
        }

        return this;
    }

    public string Build()
    {
        return _script.ToString();
    }

    public override string ToString()
    {
        return Build();
    }
}
