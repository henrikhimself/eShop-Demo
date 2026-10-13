// <copyright file="InventoryMessages.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Messaging;

public sealed record InventoryReportMessage(Guid SellerId, string Sku, int Quantity);

// See doc/CHRONICLE.md — payload shape is a placeholder pending a real Commerce Connect consumer (ADR 0009).
public sealed record InventoryResultMessage(Guid SellerId, string Sku, bool Confirmed, string? Error);
