// <copyright file="SellerProvisioner.cs" company="Henrik Jensen">
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

using System.Security.Claims;
using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hj.EShop.SellerPortal.Bff.Data;

// Admin-provisioned Seller accounts live only in the identity provider (out of band) -
// the Seller Portal never runs its own signup/approval flow. This helper is the
// just-in-time bridge: the first authenticated request from a given subject creates the
// local anchor row it needs to own Drafts/Submissions/Inventory.
internal static class SellerProvisioner
{
    public static async Task<Seller> EnsureSellerAsync(
        ClaimsPrincipal user,
        SellerPortalDbContext db,
        CancellationToken cancellationToken)
    {
        string subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The authenticated user has no subject claim.");

        Seller? seller = await db.Sellers
            .FirstOrDefaultAsync(s => s.SubjectId == subjectId, cancellationToken);

        if (seller is not null)
        {
            return seller;
        }

        seller = new Seller
        {
            Id = Guid.CreateVersion7(),
            SubjectId = subjectId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        db.Sellers.Add(seller);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two concurrent first-requests for the same subject can both insert here, but
            // the SubjectId unique index lets only one win. Reload its row instead of
            // surfacing a transient 500 for a legitimate user.
            db.Sellers.Remove(seller);
            seller = await db.Sellers.SingleAsync(s => s.SubjectId == subjectId, cancellationToken);
        }

        return seller;
    }
}
