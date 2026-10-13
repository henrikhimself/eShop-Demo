// <copyright file="StorefrontComponentParserTests.cs" company="Henrik Jensen">
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

using Xunit;

namespace Hj.EShop.StoreFront.MigrationRunner.Tests;

public sealed class StorefrontComponentParserTests
{
    [Theory]
    [InlineData("cms")]
    [InlineData("CMS")]
    public void Parse_CmsArgument_ReturnsCms(string arg)
    {
        StorefrontComponent actual = StorefrontComponentParser.Parse([arg]);

        Assert.Equal(StorefrontComponent.Cms, actual);
    }

    [Theory]
    [InlineData("commerce")]
    [InlineData("COMMERCE")]
    public void Parse_CommerceArgument_ReturnsCommerce(string arg)
    {
        StorefrontComponent actual = StorefrontComponentParser.Parse([arg]);

        Assert.Equal(StorefrontComponent.Commerce, actual);
    }

    [Fact]
    public void Parse_UnknownComponent_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorefrontComponentParser.Parse(["not-a-component"]));
    }

    [Fact]
    public void Parse_NoArguments_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorefrontComponentParser.Parse([]));
    }

    [Fact]
    public void Parse_TooManyArguments_Throws()
    {
        Assert.Throws<ArgumentException>(() => StorefrontComponentParser.Parse(["cms", "commerce"]));
    }
}
