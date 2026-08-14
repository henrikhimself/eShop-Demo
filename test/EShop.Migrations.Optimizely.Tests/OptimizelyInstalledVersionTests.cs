// <copyright file="OptimizelyInstalledVersionTests.cs" company="Henrik Jensen">
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

public sealed class OptimizelyInstalledVersionTests
{
    [Fact]
    public void Get_UnknownAssemblyName_Throws()
    {
        Assert.ThrowsAny<Exception>(() => OptimizelyInstalledVersion.Get("Not.A.Real.Assembly.Name"));
    }

    [Fact]
    public void Get_ThisAssembly_ReturnsThreePartVersionString()
    {
        // This test project's own assembly is a stand-in for a real EPiServer.* one -
        // exercises the "drop the fourth AssemblyVersion component" behavior without
        // needing a real Optimizely package reference in this unit-test project.
        string version = OptimizelyInstalledVersion.Get(typeof(OptimizelyInstalledVersionTests).Assembly.GetName().Name!);

        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }
}
