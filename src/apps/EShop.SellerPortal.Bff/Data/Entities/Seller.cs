// <copyright file="Seller.cs" company="Henrik Jensen">
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

namespace Hj.EShop.SellerPortal.Bff.Data.Entities;

// Bare identity anchor, created just-in-time on first authenticated request. Accounts
// are admin-provisioned directly in the identity provider (Keycloak locally, Microsoft
// Entra External ID once deployed - ADR 0020), out of band, so there is no local
// profile or approval-state to track beyond the link to the provider's subject.
internal sealed class Seller
{
    public required Guid Id { get; init; }

    // The OIDC "sub" claim (ClaimTypes.NameIdentifier), unique per identity provider.
    public required string SubjectId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public List<Draft> Drafts { get; init; } = [];

    public List<Submission> Submissions { get; init; } = [];

    public List<SellerInventory> Inventory { get; init; } = [];
}
