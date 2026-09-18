// <copyright file="TestSqlConnectionString.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Testing.Common;

// Shared by every Integration-tagged test needing a real, reachable SQL Server (for
// example sp_getapplock/SchemaMarkerHealthCheck, which have no SQLite/in-memory
// equivalent) - not provisioned by any suite itself. Point
// ESHOP_TEST_SQL_CONNECTION_STRING at one (for example the "sql" container's connection
// string from a running `eshop run` session) before running those tests; they skip
// themselves when it's unset, same reasoning as this repo's other Integration-tagged
// tests being opt-in only (see doc/CHRONICLE.md).
public static class TestSqlConnectionString
{
    private const string EnvironmentVariableName = "ESHOP_TEST_SQL_CONNECTION_STRING";

    public static string RequireOrSkip()
    {
        string? connectionString = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip($"Set {EnvironmentVariableName} to a real SQL Server connection string to run this test.");
        }

        return connectionString;
    }
}
