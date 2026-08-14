// <copyright file="TestJsonOptions.cs" company="Henrik Jensen">
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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hj.EShop.SellerPortal.Bff.Tests;

// ReadFromJsonAsync<T> with no explicit options uses System.Text.Json's bare default,
// which has no enum-to-string converter - it can only read an enum from its numeric
// value. The Bff serializes DraftStatus/DraftKind/MovieFormat/SubmissionStatus as
// strings (Program.cs's global JsonStringEnumConverter), so every test deserializing a
// Contract with one of those properties needs this instead of the bare default.
internal static class TestJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
