// <copyright file="E2ETestSettings.cs" company="Henrik Jensen">
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
using Spectre.Console.Cli;

namespace Hj.EShop.Cli.Commands.Test;

internal sealed class E2ETestSettings : TestSettings
{
    [CommandOption("--filter-class <CLASS>")]
    [Description("Run only methods in a fully qualified E2E test class. Wildcards are supported.")]
    public string? FilterClass { get; set; }

    [CommandOption("--filter-method <METHOD>")]
    [Description("Run one fully qualified E2E test method.")]
    public string? FilterMethod { get; set; }
}
