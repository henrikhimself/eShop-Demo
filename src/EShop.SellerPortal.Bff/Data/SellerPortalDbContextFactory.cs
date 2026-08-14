// <copyright file="SellerPortalDbContextFactory.cs" company="Henrik Jensen">
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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hj.EShop.SellerPortal.Bff.Data;

// Lets `dotnet ef migrations add` build the model without running the real Program.cs
// (needs Aspire/Keycloak configuration only present under the AppHost). The connection
// string is never used to connect - only to select the SQL Server provider.
internal sealed class SellerPortalDbContextFactory : IDesignTimeDbContextFactory<SellerPortalDbContext>
{
    public SellerPortalDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<SellerPortalDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlServer("Server=localhost;Database=seller-db;Trusted_Connection=True;");

        return new SellerPortalDbContext(optionsBuilder.Options);
    }
}
