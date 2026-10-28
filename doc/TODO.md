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
- Storefront schema migration follows [ADR 0023](./adr/0023-explicit-database-schema-migration-resources.md):
  `EShop.StoreFront.MigrationRunner` applies the Optimizely-shipped CMS and Commerce SQL
  scripts using the shared lock and marker model. Before Storefront starts, its preflight
  checks the shared schema marker instead of `.WaitFor` or a health check, because
  Optimizely's `DatabaseSchemaHost` cannot start against a missing schema and `.WaitFor`
  does not control Azure Container Apps startup.
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
- `SubmissionEndpoints.cs`'s submit and cancel-review routes commit the Draft/Submission
  state before publishing to Service Bus, then return `500` if the publish still fails
  after a bounded retry (`SendWithRetryAsync`), logging the failure. The bounded retry
  absorbs a transient broker failure (for example a brief Service Bus reconnect). The DB
  state is already durable and correct at that point, but a *persistent* broker outage
  still has no compensating transaction - the Seller only sees a generic submit failure,
  and nothing reconciles the already-committed DB state with the never-delivered
  message.
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
- The local reverse proxy's own `/health` (and `/alive`) endpoint, from
  `EShop.ServiceDefaults`' `MapDefaultEndpoints`, has no host restriction. ASP.NET
  Core's routing gives a literal route segment priority over YARP's catch-all proxy
  route, regardless of the proxy route's own `RequireHost` constraint, so
  `https://storefront.eshop.local:8443/health` (for example) hits the reverse proxy's
  own health check instead of being forwarded to the Storefront's `/health` (see
  [ADR 0025](./adr/0025-local-development-reverse-proxy.md)). Needs a decision on
  where the reverse proxy's own health endpoint should live instead (for example a
  dedicated internal-only port/host), then a `RequireHost` restriction on the proxy
  routes so no public hostname can shadow a target's own health endpoint this way.
- The Storefront's later phases are identified but not started: a custom Optimizely
  Shell tool for Merchandiser submission review (amends
  [ADR 0008](./adr/0008-submission-content-with-native-approval.md) - native approval
  alone can't handle SKU deduplication before approve/reject), then the Commerce/CMS
  content types (MovieProduct/MovieVariant, MerchandiseProduct/MerchandiseVariant -
  checkout content types stay deferred) and Storefront shell pages.
- Scaffold the search microservice first with a basic API and health checks; defer
  indexing and query endpoints until the Storefront search document shape is decided.
  Also scaffold the Profile microservice.
- Extend `eshop build`/`restore`/`format` coverage notes if `EShop.StoreFront.Web` ever
  gains its own frontend build step (it has none yet - no separate JS/TS build).
- Centralize the Storefront migration runners' script-directory configuration instead of
  repeating it in AppHost resource configuration.
- Isolate Docker socket resolver test fixtures from the real rootless socket. The
  missing-socket fallback test fails on a host with a running rootless daemon because
  another supported fallback finds the real socket. The CLI suite passes in the
  utility container without a socket mount; this does not remove the native fixture
  problem.
