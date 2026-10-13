# 0027 - Optimizely shipped SQL scripts for Storefront schema

## Status

Accepted

## Context

ADR 0023 requires explicit schema migration resources. The Storefront uses Optimizely
CMS and Commerce Connect schemas. Its normal startup must not change these schemas.

## Decision

The Storefront migration resource applies the SQL scripts shipped by the installed
Optimizely packages. It uses the lock, schema marker, retry, and readiness model from
ADR 0023.

## Consequences

- The Storefront does not use Entity Framework Core migrations for the Optimizely schemas.
- The migration resource must locate and run the shipped scripts before Storefront startup.
- Local development and deployment use the same schema-safety model.
