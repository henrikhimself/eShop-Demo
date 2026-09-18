// <copyright file="DotnetFormatReportParser.cs" company="Henrik Jensen">
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

using System.Text.Json.Nodes;

namespace Hj.EShop.Cli.Diagnostics;

// Flattens the JSON report `dotnet format analyzers --report` produces into
// "file(line,col): description" lines.
internal static class DotnetFormatReportParser
{
    public static IReadOnlyList<Diagnostic> Parse(string reportJson)
    {
        if (string.IsNullOrWhiteSpace(reportJson))
        {
            return [];
        }

        JsonArray? entries = JsonNode.Parse(reportJson)?.AsArray();
        if (entries is null)
        {
            return [];
        }

        List<Diagnostic> diagnostics = [];
        foreach (JsonNode? entry in entries)
        {
            string path = entry?["FilePath"]?.GetValue<string>() ?? string.Empty;
            JsonArray? changes = entry?["FileChanges"]?.AsArray();
            if (changes is null)
            {
                continue;
            }

            foreach (JsonNode? change in changes)
            {
                int line = change?["LineNumber"]?.GetValue<int>() ?? 0;
                int column = change?["CharNumber"]?.GetValue<int>() ?? 0;
                string description = change?["FormatDescription"]?.GetValue<string>() ?? string.Empty;
                diagnostics.Add(new Diagnostic($"{path}({line},{column}): {description}"));
            }
        }

        return diagnostics;
    }
}
