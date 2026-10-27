// <copyright file="SqlNullableValueExtensionsTests.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Migrations.Common.Tests;

// DbNullIfNull is a pure function - no database needed, unlike the rest of this project.
public sealed class SqlNullableValueExtensionsTests
{
    [Fact]
    public void DbNullIfNull_NullValue_ReturnsDbNull()
    {
        string? value = null;

        object result = value.DbNullIfNull();

        Assert.Equal(DBNull.Value, result);
    }

    [Fact]
    public void DbNullIfNull_NonNullValue_ReturnsTheValue()
    {
        object result = "hello".DbNullIfNull();

        Assert.Equal("hello", result);
    }
}
