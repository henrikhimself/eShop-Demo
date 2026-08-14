# 0023 - Explicit database schema migration resources

## Status

Accepted

## Context

More than one application replica can start at the same time.

Runtime schema changes from normal application startup can race in that case.

Azure Container Apps does not preserve Aspire local startup ordering after deployment.

Local development must exercise the same migration safety model as a deployed instance.

The exact migration artifact can differ by database technology.

## Decision

The team moves database schema changes out of normal application startup.

Each database-owning component uses an explicit migration resource.

The migration resource owns schema changes for its database.

The migration resource takes a SQL Server application lock before it changes schema.

The migration resource writes a schema marker after a successful migration.

Dependent services start normally.

Dependent services report not ready until the required marker exists.

Dependent services also report not ready while the migration lock is active.

Local development uses the same lock, marker, and readiness model as deployment.

The local model must not depend on Aspire-only completion ordering.

## Consequences

- Normal application startup no longer applies production schema changes.
- Services need readiness checks that know the required schema version.
- A failed migration can stop rollout before a dependent app becomes ready.
- The deployment process must start migration resources before dependent app revisions.
- The deployment process must observe migration resource success or failure.
- Local development can find migration orchestration problems early.
- The team still must decide the exact migration artifact for each database type.
- The team still must decide the exact deployment step that starts migration resources.
