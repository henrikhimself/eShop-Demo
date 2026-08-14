// <copyright file="SqlScriptValidatingQueryParserTests.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Migrations.Optimizely.Tests;

public sealed class SqlScriptValidatingQueryParserTests
{
    [Fact]
    public void GetValidationQuery_WithBlock_ReturnsQueryText()
    {
        using StringReader reader = new(
            "--BEGINVALIDATINGQUERY\r\n"
            + "select 1, 'Ok'\r\n"
            + "--ENDVALIDATINGQUERY\r\n"
            + "ALTER TABLE dbo.Foo ADD Bar INT NULL;\r\n");

        string? query = SqlScriptValidatingQueryParser.GetValidationQuery(reader);

        Assert.Equal("select 1, 'Ok'\r\n", query);
    }

    [Fact]
    public void GetValidationQuery_NoBlock_ReturnsNull()
    {
        using StringReader reader = new("ALTER TABLE dbo.Foo ADD Bar INT NULL;\r\n");

        string? query = SqlScriptValidatingQueryParser.GetValidationQuery(reader);

        Assert.Null(query);
    }

    [Fact]
    public void GetValidationQuery_MarkersAreCaseInsensitive_StillParses()
    {
        using StringReader reader = new(
            "--beginvalidatingquery\r\n"
            + "select 0, 'Already correct database version'\r\n"
            + "--endvalidatingquery\r\n"
            + "GO\r\n");

        string? query = SqlScriptValidatingQueryParser.GetValidationQuery(reader);

        Assert.Equal("select 0, 'Already correct database version'\r\n", query);
    }

    [Fact]
    public void GetValidationQuery_LeadingCommentLines_AreSkipped()
    {
        using StringReader reader = new(
            "-- EPiServer.Cms.Core database script--\r\n"
            + "--BEGINVALIDATINGQUERY\r\n"
            + "select 1, 'Ok'\r\n"
            + "--ENDVALIDATINGQUERY\r\n");

        string? query = SqlScriptValidatingQueryParser.GetValidationQuery(reader);

        Assert.Equal("select 1, 'Ok'\r\n", query);
    }

    [Fact]
    public void GetValidationQuery_LeavesReaderPositionedAfterEndMarker()
    {
        using StringReader reader = new(
            "--BEGINVALIDATINGQUERY\r\n"
            + "select 1, 'Ok'\r\n"
            + "--ENDVALIDATINGQUERY\r\n"
            + "GO\r\n"
            + "ALTER TABLE dbo.Foo ADD Bar INT NULL;\r\n");

        SqlScriptValidatingQueryParser.GetValidationQuery(reader);
        string? nextLine = reader.ReadLine();

        Assert.Equal("GO", nextLine);
    }
}
