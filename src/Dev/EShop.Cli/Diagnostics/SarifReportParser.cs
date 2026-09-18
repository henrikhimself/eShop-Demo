// <copyright file="SarifReportParser.cs" company="Henrik Jensen">
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

// Merges every *.sarif file the solution build produced (one per analyzer-enabled
// project) and flattens their results into "file(line,col): level rule: message"
// lines - the same shape ParseDotnetBuildOutput produces, so both can be deduped
// together.
internal static class SarifReportParser
{
    public static IReadOnlyList<Diagnostic> Parse(IEnumerable<string> sarifDocuments)
    {
        List<Diagnostic> diagnostics = [];

        foreach (string document in sarifDocuments)
        {
            var root = JsonNode.Parse(document);
            JsonArray? runs = root?["runs"]?.AsArray();
            if (runs is null)
            {
                continue;
            }

            foreach (JsonNode? run in runs)
            {
                diagnostics.AddRange(ParseRun(run));
            }
        }

        return diagnostics;
    }

    private static IEnumerable<Diagnostic> ParseRun(JsonNode? run)
    {
        JsonArray? results = run?["results"]?.AsArray();
        if (results is null)
        {
            yield break;
        }

        foreach (JsonNode? result in results)
        {
            if (result is null || IsSuppressed(result))
            {
                continue;
            }

            Diagnostic? diagnostic = TryParseResult(result);
            if (diagnostic is not null)
            {
                yield return diagnostic;
            }
        }
    }

    private static bool IsSuppressed(JsonNode result)
    {
        return result["suppressionStates"]?.AsArray() is { Count: > 0 };
    }

    private static Diagnostic? TryParseResult(JsonNode result)
    {
        JsonNode? location = result["locations"]?[0]?["resultFile"];
        string rawUri = location?["uri"]?.GetValue<string>() ?? string.Empty;
        string uri = StripUriScheme(rawUri);
        if (IsUnderObjDirectory(uri))
        {
            return null;
        }

        string rule = result["ruleId"]?.GetValue<string>() ?? result["rule"]?["id"]?.GetValue<string>() ?? "unknown";
        string message = (result["message"]?.GetValue<string>() ?? string.Empty).ReplaceLineEndings(" ");
        int line = location?["region"]?["startLine"]?.GetValue<int>() ?? 0;
        int column = location?["region"]?["startColumn"]?.GetValue<int>() ?? 0;
        string level = result["level"]?.GetValue<string>() ?? "error";

        return new Diagnostic($"{uri}({line},{column}): {level} {rule}: {message}");
    }

    private static string StripUriScheme(string uri)
    {
        int schemeEnd = uri.IndexOf("://", StringComparison.Ordinal);
        return schemeEnd < 0 ? uri : uri[(schemeEnd + "://".Length)..];
    }

    private static readonly char[] _pathSeparators = ['/', '\\'];

    private static bool IsUnderObjDirectory(string uri)
    {
        return uri.Split(_pathSeparators).Contains("obj");
    }
}
