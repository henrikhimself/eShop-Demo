// <copyright file="IOutputSink.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Output;

// The only way a command may produce output. No command calls Console.WriteLine or
// AnsiConsole.* directly - that's what keeps the human and AI output modes from ever
// drifting apart between commands. See doc/adr/0016-eshop-cli-replaces-bash-scripts.md.
internal interface IOutputSink
{
    void Heading(int level, string text);

    void Text(string text);

    void Code(string languageId, string content);

    void Table(string title, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows);

    void Status(Severity severity, string message);

    IDisposable BeginStep(string name);

    IOutputProgress BeginProgress(string description, long? total);

    // Call this right before printing a command's results (or any other static
    // content that follows a BeginStep) - HumanOutputSink's spinner doesn't end the
    // instant its last step disposes (see HumanOutputSink), so printing immediately
    // after a step without this can visually land while the spinner is still up,
    // making results look out of order or corrupting the print. No-op for AiOutputSink,
    // which has nothing to settle.
    Task FlushPendingStepsAsync();
}
