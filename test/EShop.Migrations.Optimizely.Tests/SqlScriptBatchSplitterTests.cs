// <copyright file="SqlScriptBatchSplitterTests.cs" company="Henrik Jensen">
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

public sealed class SqlScriptBatchSplitterTests
{
    [Fact]
    public void Split_MultipleBatchesSeparatedByGo_ReturnsEachBatch()
    {
        using StringReader reader = new(
            "CREATE TABLE dbo.Foo (Id INT);\r\n"
            + "GO\r\n"
            + "CREATE TABLE dbo.Bar (Id INT);\r\n"
            + "GO\r\n");

        List<string> batches = [.. SqlScriptBatchSplitter.Split(reader)];

        Assert.Equal(2, batches.Count);
        Assert.Equal("CREATE TABLE dbo.Foo (Id INT);\r\n", batches[0]);
        Assert.Equal("CREATE TABLE dbo.Bar (Id INT);\r\n", batches[1]);
    }

    [Fact]
    public void Split_TrailingBatchWithNoFinalGo_IsStillReturned()
    {
        using StringReader reader = new("CREATE TABLE dbo.Foo (Id INT);\r\n");

        List<string> batches = [.. SqlScriptBatchSplitter.Split(reader)];

        Assert.Equal(["CREATE TABLE dbo.Foo (Id INT);\r\n"], batches);
    }

    [Fact]
    public void Split_GoMarkerIsCaseInsensitive()
    {
        using StringReader reader = new(
            "CREATE TABLE dbo.Foo (Id INT);\r\n"
            + "go\r\n"
            + "CREATE TABLE dbo.Bar (Id INT);\r\n");

        List<string> batches = [.. SqlScriptBatchSplitter.Split(reader)];

        Assert.Equal(2, batches.Count);
    }

    [Fact]
    public void Split_EmptyBatchesBetweenConsecutiveGoLines_AreSkipped()
    {
        using StringReader reader = new(
            "GO\r\n"
            + "GO\r\n"
            + "CREATE TABLE dbo.Foo (Id INT);\r\n"
            + "GO\r\n");

        List<string> batches = [.. SqlScriptBatchSplitter.Split(reader)];

        Assert.Equal(["CREATE TABLE dbo.Foo (Id INT);\r\n"], batches);
    }

    [Fact]
    public void Split_BlankLinesWithinABatch_AreOmittedButBatchSurvives()
    {
        using StringReader reader = new(
            "CREATE TABLE dbo.Foo (Id INT);\r\n"
            + "\r\n"
            + "ALTER TABLE dbo.Foo ADD Bar INT NULL;\r\n"
            + "GO\r\n");

        List<string> batches = [.. SqlScriptBatchSplitter.Split(reader)];

        Assert.Equal(
            ["CREATE TABLE dbo.Foo (Id INT);\r\nALTER TABLE dbo.Foo ADD Bar INT NULL;\r\n"],
            batches);
    }
}
