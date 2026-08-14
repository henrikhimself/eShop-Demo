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

namespace Hj.EShop.SellerPortal.Bff.Tests;

// See EShop.SellerPortal.MigrationRunner.Tests' copy of this same helper - duplicated
// rather than shared, since test projects don't reference each other in this repo. Needs a
// real, reachable SQL Server (SchemaMarkerHealthCheck casts to SqlConnection, which a
// SQLite-backed test context can't satisfy).
internal static class TestSqlConnectionString
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
