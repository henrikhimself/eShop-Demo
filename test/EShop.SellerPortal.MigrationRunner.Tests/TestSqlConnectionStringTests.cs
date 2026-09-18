// <copyright file="TestSqlConnectionStringTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Testing.Common;
using Xunit;

namespace Hj.EShop.SellerPortal.MigrationRunner.Tests;

// The one non-Integration test in this project - everything else here needs a real SQL
// Server (see TestSqlConnectionString). Not tagged Integration itself: it deliberately
// never touches SQL Server, only the environment-variable lookup.
public sealed class TestSqlConnectionStringTests
{
    private const string EnvironmentVariableName = "ESHOP_TEST_SQL_CONNECTION_STRING";

    [Fact]
    public void RequireOrSkip_EnvironmentVariableSet_ReturnsItsValue()
    {
        string? previous = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        Environment.SetEnvironmentVariable(EnvironmentVariableName, "Server=test-value;");
        try
        {
            string result = TestSqlConnectionString.RequireOrSkip();

            Assert.Equal("Server=test-value;", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, previous);
        }
    }
}
