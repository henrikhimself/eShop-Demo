// <copyright file="ConsoleKeyReader.cs" company="Henrik Jensen">
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

internal sealed class ConsoleKeyReader(IConsoleInput consoleInput) : IConsoleKeyReader
{
    public async Task<char?> ReadKeyAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (consoleInput.IsInputRedirected)
            {
                return await ReadRedirectedKeyAsync(cancellationToken);
            }

            if (consoleInput.KeyAvailable)
            {
                return consoleInput.ReadKey();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        return null;
    }

    private async Task<char?> ReadRedirectedKeyAsync(CancellationToken cancellationToken)
    {
        char[] buffer = new char[1];
        Task<int> readTask = consoleInput.ReadAsync(buffer).AsTask();
        var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        Task completedTask = await Task.WhenAny(readTask, cancellationTask);
        if (completedTask == cancellationTask)
        {
            await cancellationTask;
        }

        int read = await readTask;
        return read == 0 ? null : buffer[0];
    }
}
