// <copyright file="GlobalSettings.cs" company="Henrik Jensen">
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

using System.ComponentModel;
using Hj.EShop.Cli.Execution;
using Spectre.Console.Cli;

namespace Hj.EShop.Cli;

// Every command's settings class derives from this one, so --tools/--agent/--debug are
// available (and consistently documented) everywhere. GlobalOptionsInterceptor
// resolves the actual precedence (flag > env var > default) once per invocation - see
// that class, not this one, for the resolution logic itself.
internal abstract class GlobalSettings : CommandSettings
{
    [CommandOption("--tools <MODE>")]
    [Description("Where tools run: auto (default, prefer local) | local | container.")]
    public ExecutionMode? Tools { get; set; }

    [CommandOption("--agent")]
    [Description("AI-optimized output: no color/emoji/spinners, stable status lines.")]
    public bool Agent { get; set; }

    [CommandOption("--debug")]
    [Description("Stream prefixed utility output for troubleshooting.")]
    public bool Debug { get; set; }
}
