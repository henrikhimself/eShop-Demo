// <copyright file="DotnetFormatReportParserTests.cs" company="Henrik Jensen">
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

public sealed class DotnetFormatReportParserTests
{
    [Fact]
    public void Parse_ReportWithChanges_ProducesOneLinePerChange()
    {
        string report = """
            [
              {
                "FilePath": "/repo/src/Foo.cs",
                "FileChanges": [
                  { "LineNumber": 3, "CharNumber": 1, "FormatDescription": "Fix whitespace formatting" },
                  { "LineNumber": 9, "CharNumber": 5, "FormatDescription": "Fix end of line marker" }
                ]
              }
            ]
            """;

        IReadOnlyList<Diagnostic> diagnostics = DotnetFormatReportParser.Parse(report);

        Assert.Equal(
            [
                "/repo/src/Foo.cs(3,1): Fix whitespace formatting",
                "/repo/src/Foo.cs(9,5): Fix end of line marker",
            ],
            diagnostics.Select(d => d.Text));
    }

    [Fact]
    public void Parse_EmptyReport_ReturnsEmpty()
    {
        Assert.Empty(DotnetFormatReportParser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_EmptyArrayReport_ReturnsEmpty()
    {
        Assert.Empty(DotnetFormatReportParser.Parse("[]"));
    }
}
