// <copyright file="IRunSettingsReader.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Commands;

// Reads RunCommand's extra "aspire start"/"aspire stop" environment variables from
// appsettings.json's "run:params" so they don't need to be hardcoded into RunCommand
// itself.
internal interface IRunSettingsReader
{
    Task<IReadOnlyDictionary<string, string>> GetRunEnvironmentVariablesAsync(CancellationToken cancellationToken);
}
