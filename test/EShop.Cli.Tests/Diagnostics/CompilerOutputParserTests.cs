// <copyright file="CompilerOutputParserTests.cs" company="Henrik Jensen">
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

public sealed class CompilerOutputParserTests
{
    [Fact]
    public void ParseDotnetBuildOutput_ExtractsErrorAndWarningLines()
    {
        string output =
            "Restore complete.\n"
            + "Foo.cs(42,5): error CS1002: ; expected [/repo/EShop.slnx]\n"
            + "Bar.cs(10,1): warning CS0168: variable 'x' is declared but never used [/repo/EShop.slnx]\n"
            + "Build FAILED.\n";

        IReadOnlyList<Diagnostic> diagnostics = CompilerOutputParser.ParseDotnetBuildOutput(output);

        Assert.Equal(
            [
                "Foo.cs(42,5): error CS1002: ; expected [/repo/EShop.slnx]",
                "Bar.cs(10,1): warning CS0168: variable 'x' is declared but never used [/repo/EShop.slnx]",
            ],
            diagnostics.Select(d => d.Text));
    }

    [Fact]
    public void ParseDotnetBuildOutput_ExcludesPathsUnderObjDirectory()
    {
        string output = "obj/Debug/net10.0/Foo.cs(1,1): error CS0001: generated code error\n";

        IReadOnlyList<Diagnostic> diagnostics = CompilerOutputParser.ParseDotnetBuildOutput(output);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ParseDotnetBuildOutput_StripsAnsiEscapeSequencesBeforeMatching()
    {
        string output = "[31mFoo.cs(1,1): error CS0001: broken[0m\n";

        IReadOnlyList<Diagnostic> diagnostics = CompilerOutputParser.ParseDotnetBuildOutput(output);

        Assert.Equal(["Foo.cs(1,1): error CS0001: broken"], diagnostics.Select(d => d.Text));
    }

    [Fact]
    public void ParseTypeScriptCompilerOutput_ExtractsErrorLines()
    {
        string output = "page.tsx(12,3): error TS2322: Type 'string' is not assignable to type 'number'.\n";

        IReadOnlyList<Diagnostic> diagnostics = CompilerOutputParser.ParseTypeScriptCompilerOutput(output);

        Assert.Equal(
            ["page.tsx(12,3): error TS2322: Type 'string' is not assignable to type 'number'."],
            diagnostics.Select(d => d.Text));
    }
}
