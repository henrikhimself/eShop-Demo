# 0024 - Search microservice boundary

## Status

Accepted

## Context

ADR 0022 picks Elasticsearch for local development.

ADR 0022 picks Azure AI Search for a deployed instance.

The Storefront search document shape is not yet decided.

The Storefront should not depend on backend-specific search APIs.

The team still wants an early search integration slice.

That slice must verify configuration, hosting, and health.

## Decision

The team adds a Storefront search microservice.

The Storefront calls this microservice for search capabilities.

The microservice owns provider selection.

The microservice owns backend-specific search clients.

The first slice exposes a basic API and health checks.

The first slice does not expose catalog indexing or query endpoints.

The team adds those endpoints after it decides the search document shape.

## Consequences

- The AppHost models a search microservice and a search backend.
- The search microservice uses Elasticsearch in local development.
- The search microservice uses Azure AI Search in a deployed instance.
- The Storefront does not need Elasticsearch or Azure AI Search client code.
- The service boundary can hide backend differences from the Storefront.
- The first implementation can validate runtime wiring before catalog search exists.
- The team still must decide the search document shape.
- The team still must decide indexing, query, ranking, and recommendation APIs.
