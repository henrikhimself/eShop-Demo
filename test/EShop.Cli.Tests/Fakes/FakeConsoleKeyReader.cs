// <copyright file="FakeConsoleKeyReader.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.Tests.Fakes;

// Replays a canned key sequence, then - once exhausted - blocks until the caller's own
// cancellation token fires, mirroring a real interactive session where the user simply
// hasn't pressed anything yet.
internal sealed class FakeConsoleKeyReader(params char?[] keys) : IConsoleKeyReader
{
    private int _index;

    public async Task<char?> ReadKeyAsync(CancellationToken cancellationToken)
    {
        if (_index < keys.Length)
        {
            return keys[_index++];
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);
        return null;
    }
}
