// <copyright file="SarifReportParserTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Diagnostics;
using Xunit;

namespace Hj.EShop.Cli.Tests.Diagnostics;

// Fixtures mirror the actual shape Roslyn's SARIF exporter writes in this repo (see
// test/EShop.Cli.Tests/EShop.Cli.Tests.sarif) - message is a plain string, not the
// {"text": ...} object the SARIF spec allows.
public sealed class SarifReportParserTests
{
    [Fact]
    public void Parse_UnsuppressedResult_ProducesOneLine()
    {
        string document = """
            {
              "$schema": "https://schemastore.azurewebsites.net/schemas/json/sarif-2.1.0-rtm.5.json",
              "version": "2.1.0",
              "runs": [
                {
                  "results": [
                    {
                      "ruleId": "CA1515",
                      "level": "error",
                      "message": "Foo can be made internal",
                      "locations": [
                        {
                          "resultFile": {
                            "uri": "file:///repo/src/Foo.cs",
                            "region": { "startLine": 5, "startColumn": 7 }
                          }
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        IReadOnlyList<Diagnostic> diagnostics = SarifReportParser.Parse([document]);

        Assert.Equal(["/repo/src/Foo.cs(5,7): error CA1515: Foo can be made internal"], diagnostics.Select(d => d.Text));
    }

    [Fact]
    public void Parse_SuppressedResult_IsExcluded()
    {
        string document = """
            {
              "runs": [
                {
                  "results": [
                    {
                      "ruleId": "CA1515",
                      "level": "error",
                      "message": "Foo can be made internal",
                      "suppressionStates": ["suppressedInSource"],
                      "locations": [
                        { "resultFile": { "uri": "file:///repo/src/Foo.cs", "region": { "startLine": 5, "startColumn": 7 } } }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        IReadOnlyList<Diagnostic> diagnostics = SarifReportParser.Parse([document]);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_ResultUnderObjDirectory_IsExcluded()
    {
        string document = """
            {
              "runs": [
                {
                  "results": [
                    {
                      "ruleId": "CA1515",
                      "level": "error",
                      "message": "generated code",
                      "locations": [
                        { "resultFile": { "uri": "file:///repo/obj/Debug/Foo.g.cs", "region": { "startLine": 1, "startColumn": 1 } } }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        IReadOnlyList<Diagnostic> diagnostics = SarifReportParser.Parse([document]);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_MultipleDocuments_MergesResultsFromEach()
    {
        string first = """{"runs":[{"results":[{"ruleId":"R1","level":"error","message":"m1","locations":[{"resultFile":{"uri":"file:///a.cs","region":{"startLine":1,"startColumn":1}}}]}]}]}""";
        string second = """{"runs":[{"results":[{"ruleId":"R2","level":"warning","message":"m2","locations":[{"resultFile":{"uri":"file:///b.cs","region":{"startLine":2,"startColumn":2}}}]}]}]}""";

        IReadOnlyList<Diagnostic> diagnostics = SarifReportParser.Parse([first, second]);

        Assert.Equal(
            ["/a.cs(1,1): error R1: m1", "/b.cs(2,2): warning R2: m2"],
            diagnostics.Select(d => d.Text));
    }
}
