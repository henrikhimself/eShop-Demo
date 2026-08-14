# C4 Model Diagrams

This directory holds the C4 model diagrams described in [`AGENTS.md`](../../AGENTS.md),
written as [C4-PlantUML](https://github.com/plantuml-stdlib/C4-PlantUML) `.puml`
sources, each rendered to an `.svg` image of the same name viewed directly (for example
[`system-context.svg`](./system-context.svg)).

## Diagrams

- **System Context** (C1) — [`system-context.svg`](./system-context.svg)
  ([source](./system-context.puml)). The eShop platform, its actors, and the external
  systems it talks to.
- **Container** (C2) — [`containers.svg`](./containers.svg)
  ([source](./containers.puml)). The deployable applications and data stores inside
  eShop, and how they connect.
- **Component** (C3) — [`components/`](./components). The internal building blocks of
  one container. A component diagram needs more implementation-level detail than the
  Context or Container level, so one exists only where the specification or an ADR
  already gives that detail. See the index below.
- **Code** (C4) — out of scope. This level maps to source code (classes, interfaces)
  and is better read directly from the code than kept as a hand-written diagram.

## Component diagrams

- [`components/seller-submission-review.svg`](./components/seller-submission-review.svg)
  ([source](./components/seller-submission-review.puml)) — the Seller Portal submission
  and Merchandiser review mechanism inside the Storefront container (see ADR 0008 and
  ADR 0009).

See [`TODO.md`](../TODO.md) for the component diagrams still to create, once the
specification gives enough detail about the rest of the Storefront, the Seller Portal,
and the Profile microservice.

## Rendering

See `AGENTS.md`'s "Common Commands" for how to render a `.puml` source.
