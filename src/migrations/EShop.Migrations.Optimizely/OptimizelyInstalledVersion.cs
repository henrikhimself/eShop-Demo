// <copyright file="OptimizelyInstalledVersion.cs" company="Henrik Jensen">
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

using System.Reflection;

namespace Hj.EShop.Migrations.Optimizely;

// See doc/CHRONICLE.md — every assembly in an Optimizely release train ships the same version as the NuGet package, so it's read from the assembly instead of duplicated as a config literal.
public static class OptimizelyInstalledVersion
{
    public static string Get(string assemblyName)
    {
        Version version = Assembly.Load(assemblyName).GetName().Version
            ?? throw new InvalidOperationException($"Could not determine the installed version of '{assemblyName}'.");

        return new Version(version.Major, version.Minor, version.Build).ToString();
    }
}
