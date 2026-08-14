# 0021 - Redis, not Valkey, for the cache and Data Protection key ring

## Status

Accepted

Supersedes [ADR 0012](./0012-valkey-for-ticket-store-and-data-protection.md). ADR 0012
picked Valkey over Redis because Redis's license (RSALv2/SSPLv1) is not an OSI-approved
open source license. This ADR reverses that choice.

## Context

ADR 0018 picked Azure Container Apps as the deployment target. ADR 0018 left open
whether a backing service without a managed Azure equivalent stays a plain container in
a deployed instance. Valkey has no managed Azure equivalent. Redis has one: Azure
Cache for Redis. Aspire has a hosting integration for Azure Cache for Redis, through
the `Aspire.Hosting.Azure.Redis` package.

Redis 8.0 also added the AGPLv3 license as a licensing choice, alongside its existing
RSALv2 and SSPLv1 choices. AGPLv3 is an OSI-approved open source license.

The team no longer needs to make a license-interpretation judgment call about
RSALv2/SSPLv1 compliance for the team's own use of Redis. Azure Cache for Redis is a
managed Microsoft Azure service. Microsoft, not the team, operates the underlying
Redis software and carries the licensing obligation that comes with operating it. This
pushes the licensing question behind the Microsoft Azure terms of use. The team's own
obligation becomes compliance with those terms, a normal condition of using any Azure
service, not a Redis-specific licensing question.

## Decision

The team picks Redis, through Aspire's Azure Cache for Redis hosting integration, as
the product behind the Seller Portal's auth ticket store and Data Protection key ring.
This replaces Valkey, for local development and for a deployed instance alike. The
AppHost adds this service through `AddAzureManagedRedis`. The AppHost runs a local
Redis container during local development, through the same resource's `RunAsContainer`
method.

## Consequences

- The AppHost no longer defines a Valkey resource. Local development now runs a plain
  Redis container instead. A deployed instance gets a real Azure Cache for Redis
  instance instead of a plain container inside the Azure Container Apps environment.
- The Seller Portal Bff needs no code change beyond the resource's connection name. The
  Bff already connects through `Aspire.StackExchange.Redis`, a client package that
  works unchanged against Redis, Valkey, and Azure Cache for Redis.
- `HybridCacheTicketStore` and the Data Protection key ring's `RedisXmlRepository` keep
  working exactly as ADR 0012 described; only the product behind the connection
  changes.
- The team's compliance obligation for the deployed instance's cache is the Microsoft
  Azure terms of use, not Redis's own license terms. The team does not track Redis's
  license choice (RSALv2, SSPLv1, or AGPLv3) separately for that instance.
