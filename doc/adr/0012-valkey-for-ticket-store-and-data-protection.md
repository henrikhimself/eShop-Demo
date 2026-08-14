# 0012 - Valkey for the Seller Portal's auth ticket store and Data Protection key ring

## Status

Accepted

Superseded by [ADR 0021](./0021-redis-replaces-valkey.md). ADR 0021 replaces Valkey
with Redis, for local development and for a deployed instance alike.

## Context

The Seller Portal BFF needs a server-side store for its authentication ticket, so
the `seller-portal` cookie itself stays small. This store needs a distributed
second-level cache behind `HybridCache`'s in-process first-level cache.

The BFF also needs one persisted store for its Data Protection key ring. The team
wants one shared piece of infrastructure for both needs, not two.

Redis is a common product for a `HybridCache` second-level cache. ASP.NET Core Data
Protection also ships its own Redis-backed key store
(`Microsoft.AspNetCore.DataProtection.StackExchangeRedis`). Redis's own license
(RSALv2/SSPLv1) is not an OSI-approved open source license. Valkey is a Linux
Foundation fork of Redis, under the OSI-approved BSD-3-Clause license, that keeps
the Redis wire protocol - a client written for Redis works against Valkey without
change.

## Decision

The team picks Valkey as the product behind the Seller Portal's auth ticket store
and Data Protection key ring, instead of Redis itself. The eShop AppHost hosts a
Valkey container for local development, through the `Aspire.Hosting.Valkey`
package.

The Bff project connects to this Valkey container through the same
Redis-protocol client packages a Redis-backed setup would use:
`Aspire.StackExchange.Redis`, `Aspire.StackExchange.Redis.DistributedCaching`, and
`Microsoft.AspNetCore.DataProtection.StackExchangeRedis`. Valkey's wire
compatibility with Redis means none of this client-side code depends on which of
the two products runs the server.

## Consequences

- The eShop AppHost defines a Valkey resource with a persistent data volume. Both
  the ticket store and the key ring survive an `aspire start` restart.
- `HybridCacheTicketStore`
  (`src/EShop.SellerPortal.Bff/Authentication/HybridCacheTicketStore.cs`)
  implements `ITicketStore` over `HybridCache`. The `seller-portal` cookie now
  holds only this store's opaque key, never the whole ticket.
- The Data Protection key ring now persists into Valkey, through a
  `RedisXmlRepository`.
