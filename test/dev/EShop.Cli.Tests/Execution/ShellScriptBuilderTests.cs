// <copyright file="ShellScriptBuilderTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Execution;
using Xunit;

namespace Hj.EShop.Cli.Tests.Execution;

public sealed class ShellScriptBuilderTests
{
    [Fact]
    public void Append_LineWithoutNewline_AddsOne()
    {
        string script = new ShellScriptBuilder().Append("set -eu").Build();

        Assert.Equal("set -eu\n", script);
    }

    [Fact]
    public void Append_LineWithNewline_DoesNotDoubleIt()
    {
        string script = new ShellScriptBuilder().Append("set -eu\n").Build();

        Assert.Equal("set -eu\n", script);
    }

    [Theory]
    [InlineData("set -eu\r\n")]
    [InlineData("set -eu\r")]
    public void Append_CarriageReturnLineEndings_AreConvertedToNewline(string line)
    {
        string script = new ShellScriptBuilder().Append(line).Build();

        Assert.Equal("set -eu\n", script);
    }

    [Fact]
    public void Append_MultiLineText_NormalizesEveryLineEnding()
    {
        string script = new ShellScriptBuilder().Append("a\r\nb\rc\nd").Build();

        Assert.Equal("a\nb\nc\nd\n", script);
    }

    [Fact]
    public void Append_EmptyLine_WritesOneBlankLine()
    {
        string script = new ShellScriptBuilder().Append("a").Append(string.Empty).Append("b").Build();

        Assert.Equal("a\n\nb\n", script);
    }

    [Fact]
    public void Append_InterpolatedLineWithLiteralBraces_IsAppendedUnchanged()
    {
        string name = "hj_stop";

        string script = new ShellScriptBuilder().Append($"{name}() {{").Append("  docker ps --format '{{.Names}}'").Build();

        Assert.Equal("hj_stop() {\n  docker ps --format '{{.Names}}'\n", script);
    }

    [Fact]
    public void Append_Chained_KeepsOrder()
    {
        ShellScriptBuilder builder = new();

        string script = builder.Append("one").Append("two").Append("three").Build();

        Assert.Equal("one\ntwo\nthree\n", script);
        Assert.Equal(script, builder.ToString());
    }

    [Theory]
    [InlineData("plain", "'plain'")]
    [InlineData("with space", "'with space'")]
    [InlineData("it's", "'it'\\''s'")]
    public void Quote_EscapesForPosixShell(string value, string expected)
    {
        Assert.Equal(expected, ShellScriptBuilder.Quote(value));
    }

    [Fact]
    public void Build_MixedInput_NeverContainsCarriageReturn()
    {
        string script = new ShellScriptBuilder().Append("a\r\nb").Append("c\r").Append("d").Build();

        Assert.DoesNotContain('\r', script);
    }
}
