// <copyright file="SqlScriptValidatingQueryParser.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Migrations.Optimizely;

// See doc/CHRONICLE.md — every Optimizely schema script opens with a --BEGINVALIDATINGQUERY/--ENDVALIDATINGQUERY block.
// Reads from a shared TextReader (not a file path) so OptimizelySqlScriptRunner can continue reading the script body from the exact position this parser stopped at.
public static class SqlScriptValidatingQueryParser
{
    private const string BeginMarker = "--BEGINVALIDATINGQUERY";
    private const string EndMarker = "--ENDVALIDATINGQUERY";

    // Returns null if the script has no validating-query block at all; callers decide what that means (this repo treats it as an error, not "run unconditionally").
    public static string? GetValidationQuery(TextReader reader)
    {
        StringBuilder query = new();
        string? line = reader.ReadLine();
        while (line is not null)
        {
            string trimmed = line.Trim();
            if (trimmed.Equals(BeginMarker, StringComparison.OrdinalIgnoreCase))
            {
                return ReadUntilEndMarker(reader, query);
            }

            if (trimmed.StartsWith("--", StringComparison.Ordinal) || trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                line = reader.ReadLine();
                continue;
            }

            break;
        }

        return null;
    }

    private static string ReadUntilEndMarker(TextReader reader, StringBuilder query)
    {
        string? line = reader.ReadLine();
        while (line is not null)
        {
            if (line.Trim().Equals(EndMarker, StringComparison.OrdinalIgnoreCase))
            {
                return query.ToString();
            }

            if (line.Length > 0)
            {
                query.Append(line).Append("\r\n");
            }

            line = reader.ReadLine();
        }

        return query.ToString();
    }
}
