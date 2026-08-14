# eShop TODO

This document tracks questions raised while defining the business requirements
([`SPEC.md`](./SPEC.md)) and system landscape ([`System landscape.md`](./System%20landscape.md))
so they are not forgotten as the discussion continues.

## Open questions

Questions we intend to address as the discussion continues.

- Whether Optimizely's native approval system's role-based reviewer validation is
  compatible with the external-OIDC-only identity decision (see
  [ADR 0002](./adr/0002-external-oidc-identity-provider.md)). Raise this before
  implementing [ADR 0008](./adr/0008-submission-content-with-native-approval.md).

## Deferred questions

Concrete implementation-level questions that have been intentionally postponed until
they are needed to illuminate more of the system landscape, rather than decided
prematurely.

- Exact data ownership split between Commerce Connect Customer Management and the
  Profile microservice (see
  [ADR 0004](./adr/0004-profile-microservice-for-shopper-data-gaps.md)).
- Exact matching logic used by the automatic deduplication process (see
  [ADR 0006](./adr/0006-marketplace-competing-offers-shared-sku.md)).
- How the Storefront presents more than one Seller's competing offer on the same SKU to
  the Shopper, for example a list of offers or a single best-price offer (see
  [ADR 0006](./adr/0006-marketplace-competing-offers-shared-sku.md)).
- Exact message payload shape for the Seller Portal and Commerce Connect integration
  (see [ADR 0009](./adr/0009-durable-messaging-seller-portal-commerce-connect.md)). The
  queue and container naming is already decided in code.
- ADR 0009 names "Rejected or Published" as a submission's final states, for temporary
  image cleanup. The code's own submission status values treat only Approved as final
  (Rejected returns a draft to the Seller for editing, so it is not final); the
  temporary-image cleanup that already exists follows the code's own final-state
  handling, not the ADR's literal wording. Reconcile the wording, or confirm the code's
  handling is what was actually intended.
- Migration artifact and runner path for the Optimizely CMS and Commerce databases (see
  [ADR 0023](./adr/0023-explicit-database-schema-migration-resources.md)) is decided,
  implemented, and wired into `AppHost.cs`: `EShop.StoreFront.MigrationRunner` applies
  Optimizely's own shipped SQL scripts directly (raw ADO.NET, the same lock/marker model
  as `EShop.SellerPortal.MigrationRunner`'s EF-based approach). Verified end to end
  against a real SQL Server using the real `EPiServer.CMS.Core`/`EPiServer.Commerce.Core`
  scripts (not just synthetic fixtures), and inside a real `eshop run` session - see
  `doc/CHRONICLE.md`. `EShop.StoreFront.Web`'s own readiness against a not-yet-migrated
  database is resolved too: a pre-flight check against the shared schema marker, not
  `.WaitFor` or a health check (Optimizely's own `DatabaseSchemaHost` crashes the app at
  startup on a missing schema, and `.WaitFor` has no effect once deployed to Azure
  Container Apps) - also verified live.
- Exact deployment step that starts migration resources and observes their completion
  before dependent app revisions become active (see
  [ADR 0023](./adr/0023-explicit-database-schema-migration-resources.md)) - not yet
  built for `EShop.SellerPortal.MigrationRunner` either; today it only runs as an
  Aspire-orchestrated local resource.
- The Storefront search document shape, index schema, ranking rules, and embedding
  model (see [ADR 0022](./adr/0022-storefront-search-service.md) and
  [ADR 0024](./adr/0024-search-microservice-boundary.md)).
- The Storefront search microservice's catalog indexing and query endpoint shapes,
  once the team decides the search document shape.
- Whether the Storefront uses one hidden submission content type or separate hidden
  submission content types for movies and merchandise, because each kind can need its
  own editor descriptors and UI descriptors.
- The Seller Portal must support merchandise variants before it can submit merchandise
  such as T-shirts with sizes.
- Which CMS content types, if any, checkout needs. Checkout is deferred until the team
  designs that flow.
- `SubmissionEndpoints.cs`'s submit route commits the Draft/Submission state before
  publishing the `SubmissionRequestMessage`, then returns `500` if the publish itself
  throws, logging the failure. The DB state is already durable and correct at that
  point, but nothing retries the publish and the Seller only sees a generic submit
  failure - no compensating-transaction or retry logic exists yet for this gap.
- Component (C3) diagrams still to create under [`doc/c4/components`](./c4/components),
  once the specification gives enough detail:
  - The Storefront catalog and campaign flow. Checkout is deferred.
  - The Storefront search microservice, once indexing and query responsibilities are
    decided.
  - The Profile microservice internals.
  - The rest of the Seller Portal (draft management, submission UI), beyond the
    messaging component already shown in
    [`doc/c4/components/seller-submission-review.svg`](./c4/components/seller-submission-review.svg).
    Movie draft CRUD/image/submission endpoints and pages now exist, so this diagram is
    addressable - still a follow-up, not done in this round.
- `eshop run`/`eshop test e2e` need an x86-64 (amd64) host, because the SQL Server
  container image is amd64-only. Docker Desktop's Rosetta-based amd64 emulation may let
  an Apple Silicon (arm64) Mac run this path anyway, but nobody on the team has verified
  it - a Mac-using contributor should try it and report back whether it actually works,
  and how well it performs. This is a different case from a Linux arm64 devbox using
  binfmt/QEMU emulation, which is already confirmed not to work.
- `EShop.Cli` supports Linux only, for now (see
  [ADR 0017](./adr/0017-linux-only-eshop-cli-for-now.md)). Re-adding Windows and/or
  macOS support is a deliberate future task, not yet scheduled.
- The exact Microsoft Entra External ID tenant/app-registration/user-flow setup steps
  (see [ADR 0020](./adr/0020-entra-external-id-production-identity-provider.md)). The
  decision to do this by hand, in the Microsoft Entra admin center, is made; the
  step-by-step is not written down anywhere yet.
- The Bff no longer migrates its own database at startup (ADR 0023's
  `EShop.SellerPortal.MigrationRunner` does, lock-protected), closing the scaled-out
  replica race this used to track locally. What's left is deployment automation: ADR
  0018's Azure Container Apps target has no built-in way to run that migration runner
  as a gating job before a Bff revision activates - see the ADR 0023 items above.
- Two `Directory.Packages.props` pins currently sit ahead of
  [ADR 0013](./adr/0013-pinned-dependency-versions-and-40-day-quarantine.md)'s 40-day
  quarantine on purpose, as a documented security exception (see `doc/CHRONICLE.md`
  and `doc/MEMORY.md`'s "Non-obvious current constraints"): `Microsoft.AspNetCore.DataProtection.StackExchangeRedis`
  (10.0.10, since the quarantine-compliant 10.0.9 transitively pulls in a
  `System.Security.Cryptography.Xml` version with five disclosed high-severity CVEs)
  and `SQLitePCLRaw.bundle_e_sqlite3` (2.1.12, since the quarantine-compliant 2.1.11 has
  a disclosed high-severity CVE, GHSA-2m69-gcr7-jv3q). Revisit each once a version
  exists that both fixes the vulnerability and clears the 40-day quarantine.
