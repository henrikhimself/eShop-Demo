// <copyright file="SqlStatusCode.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Migrations.Optimizely;

// The status codes an Optimizely SQL script's own validating query returns (the first column between --BEGINVALIDATINGQUERY and --ENDVALIDATINGQUERY).
// See doc/CHRONICLE.md — values mirror Optimizely's own convention exactly.
public enum SqlStatusCode
{
    // The validating query returned something other than -1, 0, or 1 - never expected
    // from a genuine Optimizely script, but a script this code doesn't recognize should
    // fail loudly rather than silently run or silently skip.
    Undefined = -2,

    // The database is in a state this script doesn't expect (wrong prior version, or not
    // an Optimizely database at all) - abort rather than risk corrupting the schema.
    Invalid = -1,

    // The database is already at or ahead of this script's target version - skip it.
    AlreadyIn = 0,

    // The database is at the exact prior version this script expects - run it.
    Valid = 1,
}
