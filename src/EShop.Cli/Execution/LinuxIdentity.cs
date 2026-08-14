// <copyright file="LinuxIdentity.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Execution;

// DockerSocketGid is null either when the socket can't be stat'd (e.g. Docker not
// running yet) or, under rootless Docker, deliberately - see LinuxIdentityProvider.
// Either way, IContainerRunner omits --group-add rather than fail outright.
internal sealed record LinuxIdentity(int Uid, int Gid, int? DockerSocketGid);
