// <copyright file="ScreenshotSettings.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Commands;

internal sealed class ScreenshotSettings : DefaultSettings
{
    [CommandArgument(0, "<URL>")]
    [Description("URL to load (http://localhost:<port>/<path>, or a data: URL).")]
    public string Url { get; set; } = string.Empty;

    [CommandOption("-o|--output <PATH>")]
    [Description("Output PNG path, relative to the repo root. Default: tmp/screenshot.png.")]
    public string OutputPath { get; set; } = "tmp/screenshot.png";

    [CommandOption("--width <PIXELS>")]
    [Description("Viewport width. Default: 1440.")]
    public int Width { get; set; } = 1440;

    [CommandOption("--height <PIXELS>")]
    [Description("Viewport height. Default: 900.")]
    public int Height { get; set; } = 900;

    [CommandOption("--login")]
    [Description("Log in via Keycloak before navigating to <URL>.")]
    public bool Login { get; set; }

    [CommandOption("--login-path <PATH>")]
    [Description("Same-origin absolute path that starts login. Required when --login is set.")]
    public string? LoginPath { get; set; }

    [CommandOption("--username <USERNAME>")]
    [Description("Username for --login. Required when --login is set.")]
    public string? Username { get; set; }

    [CommandOption("--password <PASSWORD>")]
    [Description("Password for --login. Required when --login is set.")]
    public string? Password { get; set; }
}
