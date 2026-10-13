// <copyright file="ConsoleInput.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.AppHost;

internal sealed class ConsoleInput : IConsoleInput
{
    public bool IsInputRedirected => Console.IsInputRedirected;

    public bool KeyAvailable => Console.KeyAvailable;

    public char ReadKey()
    {
        return Console.ReadKey(intercept: true).KeyChar;
    }

    public ValueTask<int> ReadAsync(Memory<char> buffer)
    {
        return Console.In.ReadAsync(buffer, CancellationToken.None);
    }
}
