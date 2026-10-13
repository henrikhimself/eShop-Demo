// <copyright file="SqlValidationStatus.cs" company="Henrik Jensen">
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

// The result of running a script's own validating query - StatusMessage is whatever
// human-readable text the query's second column returns (e.g. "Upgrading database",
// "Not an EPiServer database"), used only for logging.
public sealed record SqlValidationStatus(SqlStatusCode StatusCode, string? StatusMessage);
