// <copyright file="SqlStatusCodeTests.cs" company="Henrik Jensen">
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

// Pins these values to Optimizely's own convention (confirmed by decompiling
// EPiServer.Net.Cli) - a script's validating query returns one of these raw integers, so
// a silent renumbering here would misinterpret every script's own status.
public sealed class SqlStatusCodeTests
{
    [Fact]
    public void Invalid_HasExpectedValue()
    {
        Assert.Equal(-1, (int)SqlStatusCode.Invalid);
    }

    [Fact]
    public void AlreadyIn_HasExpectedValue()
    {
        Assert.Equal(0, (int)SqlStatusCode.AlreadyIn);
    }

    [Fact]
    public void Valid_HasExpectedValue()
    {
        Assert.Equal(1, (int)SqlStatusCode.Valid);
    }
}
