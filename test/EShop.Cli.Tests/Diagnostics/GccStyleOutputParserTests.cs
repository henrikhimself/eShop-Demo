// <copyright file="GccStyleOutputParserTests.cs" company="Henrik Jensen">
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

public sealed class GccStyleOutputParserTests
{
    [Fact]
    public void Parse_ShellcheckGccFormat_ExtractsDiagnosticLines()
    {
        string output =
            "In scripts/build.bash line 10:\n"
            + "scripts/build.bash:10:5: warning: X_CURRENT_DIR appears unused [SC2034]\n"
            + "\n"
            + "For more information:\n";

        IReadOnlyList<Diagnostic> diagnostics = GccStyleOutputParser.Parse(output);

        Assert.Equal(
            ["scripts/build.bash:10:5: warning: X_CURRENT_DIR appears unused [SC2034]"],
            diagnostics.Select(d => d.Text));
    }

    [Fact]
    public void Parse_NoMatchingLines_ReturnsEmpty()
    {
        IReadOnlyList<Diagnostic> diagnostics = GccStyleOutputParser.Parse("no issues found\n");

        Assert.Empty(diagnostics);
    }
}
