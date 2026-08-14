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

// Every assembly in a given Optimizely CMS/Commerce Connect release train ships with the
// exact same version number as the NuGet package itself - confirmed by inspecting the
// pinned packages directly: pinning EPiServer.CMS to 13.0.2 resolves EPiServer.Data,
// EPiServer.Events, EPiServer.ApplicationModules, etc. to exactly 13.0.2 too (their own
// NU1608 restore messages listed dozens of them, all at that one version); the same holds
// for EPiServer.Commerce 15.1.0 and Mediachase.Commerce.dll. So the pinned version can be
// read directly from any already-referenced assembly in that train at runtime, instead of
// duplicating it as a literal in configuration on top of the Directory.Packages.props pin
// - see doc/CHRONICLE.md.
public static class OptimizelyInstalledVersion
{
    public static string Get(string assemblyName)
    {
        Version version = Assembly.Load(assemblyName).GetName().Version
            ?? throw new InvalidOperationException($"Could not determine the installed version of '{assemblyName}'.");

        // NuGet package versions are three-part (e.g. "13.0.2"); assembly versions carry
        // a fourth (revision) component that's always 0 for these packages - drop it so
        // this matches the version string the migration runner writes as SchemaVersion.
        return new Version(version.Major, version.Minor, version.Build).ToString();
    }
}
