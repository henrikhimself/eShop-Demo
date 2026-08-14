# 0022 - Storefront search service

## Status

Accepted

## Context

The Shopper can search the catalog by name and by year of release. The Shopper can
browse the catalog by genre.

The team wants the Storefront to use a real search engine. The team does not want the
default Commerce Connect Lucene search provider to be the primary search engine for the
demo.

The team also wants the search choice to support later AI features. These features can
include vector search, hybrid search, and recommendations.

Azure AI Search is a managed Azure service. It supports full-text search, filters,
facets, vector search, and hybrid search. It also fits the deployed Azure landscape.

Azure AI Search has no local emulator. The local development environment needs a search
engine that Aspire can host and observe.

Elasticsearch has an Aspire hosting integration. It can run as a local development
container. It supports full-text search, filters, facets, vector search, and hybrid
search.

## Decision

The team picks Elasticsearch as the Storefront search engine for local development.

The team picks Azure AI Search as the managed Storefront search service for a deployed
instance.

The Storefront search document is the stable contract between the catalog and the search
backend. The backend-specific query and index code can differ between local development
and a deployed instance.

## Consequences

- The AppHost hosts Elasticsearch for local development.
- The AppHost provisions or references Azure AI Search for a deployed instance.
- The Storefront must publish catalog data into a search index.
- The local search behavior can differ from the deployed search behavior.
- The team must keep the indexed document shape common across both search backends.
- The team has not yet decided the exact index schema, ranking rules, or embedding
  model.
