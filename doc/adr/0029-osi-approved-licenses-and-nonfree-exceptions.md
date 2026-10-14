# 0029 - OSI-approved licenses and nonfree exceptions

## Status

Accepted

## Context

The project normally uses third-party products under an OSI-approved license. ADR 0012
used this rule when it selected Valkey instead of the Redis license options that were
then available. ADR 0021 later made a Redis-specific exception.

Some required products have a non-free license. A directory name and project description
must make a direct dependency visible to a developer before the developer uses, copies,
or changes the project. A project with a non-free dependency must not look like a project
that has only OSI-approved dependencies.

## Decision

The project accepts third-party products only under an OSI-approved license, unless this
ADR gives an exception.

A project that directly consumes a non-free component can use that component only when
its project directory is under `src/nonfree`. Its README file must name the paid product
and state which dependency, feature, or service makes the project a paid-product consumer.

This ADR does not apply to a project that only transitively depends on a non-free
component through a project reference. The `nonfree` directory does not make a non-free
product free. It marks the direct license boundary for developers. The team must still
obtain and maintain the required product license.

## Consequences

- A developer can identify a non-free dependency from the project path and README file.
- A new project that directly consumes a non-free product must be under `src/nonfree` and
  add the required README statement before use.
- A project outside `src/nonfree` must not directly add a non-free product dependency.
- A project that uses a non-free product only through a project reference does not move
  because of this ADR.
- This decision does not replace legal review, vendor terms, or license compliance work.
- ADR 0021 remains the decision for Redis. This ADR makes the license policy explicit for
  future product selections.
