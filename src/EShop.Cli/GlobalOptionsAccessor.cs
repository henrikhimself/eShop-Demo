// <copyright file="GlobalOptionsAccessor.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli;

// One instance, registered in Program.cs via RegisterInstance for both this concrete
// type and IGlobalOptionsAccessor, so GlobalOptionsInterceptor (which writes, once,
// per invocation) and every command (which only reads) share the same object.
internal sealed class GlobalOptionsAccessor : IGlobalOptionsAccessor
{
    private GlobalOptions? _options;

    public GlobalOptions Options =>
        _options ?? throw new InvalidOperationException("Global options have not been resolved yet.");

    public void Resolve(GlobalOptions options)
    {
        if (_options is not null)
        {
            throw new InvalidOperationException("Global options have already been resolved for this invocation.");
        }

        _options = options;
    }
}
