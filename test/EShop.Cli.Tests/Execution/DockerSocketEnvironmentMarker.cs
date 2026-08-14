// <copyright file="DockerSocketEnvironmentMarker.cs" company="Henrik Jensen">
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

using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Hj.EShop.Cli.Tests.Execution;

// ESHOP_DOCKER is process-global state (Environment.SetEnvironmentVariable). Test
// classes are otherwise parallelized per-class by default in this project, so any test
// that mutates it must be serialized against every test that assumes the default
// (unset) value - DockerSocketPathResolverTests, LinuxIdentityProviderTests, and
// ContainerRunnerTests all opt into this collection rather than disabling
// parallelization assembly-wide.
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage("Design", "CA1515", Justification = "xUnit1027 requires collection definition classes to be public.")]
public static class DockerSocketEnvironmentMarker
{
    public const string Name = "DockerSocketEnvironment";
}
