// <copyright file="SchemaMarkerTests.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Migrations.Orchestration.Tests;

public sealed class SchemaMarkerTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        DateTime completedAtUtc = DateTime.UtcNow;
        SchemaMarker first = new("component", "1.0.0", completedAtUtc, Succeeded: true, FailureMessage: null);
        SchemaMarker second = new("component", "1.0.0", completedAtUtc, Succeeded: true, FailureMessage: null);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Equality_DifferentSchemaVersion_AreNotEqual()
    {
        DateTime completedAtUtc = DateTime.UtcNow;
        SchemaMarker first = new("component", "1.0.0", completedAtUtc, Succeeded: true, FailureMessage: null);
        SchemaMarker second = new("component", "2.0.0", completedAtUtc, Succeeded: true, FailureMessage: null);

        Assert.NotEqual(first, second);
    }
}
