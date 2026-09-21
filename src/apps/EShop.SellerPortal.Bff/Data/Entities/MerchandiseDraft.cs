// <copyright file="MerchandiseDraft.cs" company="Henrik Jensen">
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

// Field list per SPEC.md "Product data" (merchandise).
internal sealed class MerchandiseDraft : Draft
{
    public required string ProductName { get; set; }

    public required string Description { get; set; }

    public required decimal Price { get; set; }

    // Free text, not a foreign key: SPEC.md is explicit the associated movie may not
    // exist as a catalog product yet - the Seller identifies it by title only.
    public string? AssociatedMovieTitle { get; set; }
}
