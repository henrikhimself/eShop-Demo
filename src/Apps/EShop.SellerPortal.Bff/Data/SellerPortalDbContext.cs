// <copyright file="SellerPortalDbContext.cs" company="Henrik Jensen">
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

using Hj.EShop.SellerPortal.Bff.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hj.EShop.SellerPortal.Bff.Data;

internal sealed class SellerPortalDbContext(DbContextOptions<SellerPortalDbContext> options)
    : DbContext(options)
{
    public DbSet<Seller> Sellers => Set<Seller>();

    public DbSet<Draft> Drafts => Set<Draft>();

    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<SellerInventory> SellerInventories => Set<SellerInventory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Seller>(seller =>
        {
            seller.HasIndex(s => s.SubjectId).IsUnique();
        });

        // TPH: one Drafts table, discriminated by draft kind.
        modelBuilder.Entity<Draft>()
            .HasDiscriminator<string>("DraftKind")
            .HasValue<MovieDraft>(nameof(DraftKind.Movie))
            .HasValue<MerchandiseDraft>(nameof(DraftKind.Merchandise));

        modelBuilder.Entity<Draft>(draft =>
        {
            draft.HasOne(d => d.Seller)
                .WithMany(s => s.Drafts)
                .HasForeignKey(d => d.SellerId)
                .OnDelete(DeleteBehavior.Cascade);

            // ValueGeneratedNever required for a client-generated (Guid.CreateVersion7)
            // owned-entity key - see doc/CHRONICLE.md. Any new owned entity needs this too.
            draft.OwnsMany(d => d.Images, image =>
            {
                image.WithOwner().HasForeignKey(i => i.DraftId);
                image.HasKey(i => i.Id);
                image.Property(i => i.Id).ValueGeneratedNever();
            });
        });

        modelBuilder.Entity<MovieDraft>()
            .OwnsMany(d => d.FormatVariants, variant =>
            {
                variant.WithOwner().HasForeignKey(v => v.DraftId);
                variant.HasKey(v => v.Id);
                variant.Property(v => v.Id).ValueGeneratedNever();
            });

        modelBuilder.Entity<Submission>(submission =>
        {
            // Restrict, not Cascade - see doc/CHRONICLE.md (SQL Server rejects multiple
            // cascade paths reaching Submissions; also matches intent).
            submission.HasOne<Seller>()
                .WithMany(s => s.Submissions)
                .HasForeignKey(s => s.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            submission.HasOne<Draft>()
                .WithMany()
                .HasForeignKey(s => s.DraftId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SellerInventory>(inventory =>
        {
            inventory.HasIndex(i => new { i.SellerId, i.Sku }).IsUnique();

            inventory.HasOne<Seller>()
                .WithMany(s => s.Inventory)
                .HasForeignKey(i => i.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
