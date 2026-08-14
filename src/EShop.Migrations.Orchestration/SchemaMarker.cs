// <copyright file="SchemaMarker.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Migrations.Orchestration;

// One component's latest migration attempt - written by a migration runner, read by the
// component's own readiness check. SchemaVersion is whatever version identifier that
// runner's own migration artifact uses (an EF Core migration Id, an Optimizely package
// version string, etc.) - this type itself has no opinion on the scheme.
public sealed record SchemaMarker(string Component, string SchemaVersion, DateTime CompletedAtUtc, bool Succeeded, string? FailureMessage);
