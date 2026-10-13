// <copyright file="ConsoleKeyReaderTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.AppHost;
using Xunit;

namespace Hj.EShop.Cli.Tests.AppHost;

public sealed class ConsoleKeyReaderTests
{
    [Fact]
    public async Task ReadKeyAsync_RedirectedInputBlocks_CancellationCompletesPromptly()
    {
        BlockingConsoleInput consoleInput = new();
        ConsoleKeyReader reader = new(consoleInput);
        using CancellationTokenSource cancellation = new();

        Task<char?> readTask = reader.ReadKeyAsync(cancellation.Token);
        await consoleInput.ReadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readTask);
    }

    private sealed class BlockingConsoleInput : IConsoleInput
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsInputRedirected => true;

        public bool KeyAvailable => false;

        public char ReadKey()
        {
            throw new NotSupportedException();
        }

        public ValueTask<int> ReadAsync(Memory<char> buffer)
        {
            ReadStarted.TrySetResult();
            return new ValueTask<int>(new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously).Task);
        }
    }
}
