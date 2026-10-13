# eShop Chronicle

This file retains historical reasoning that can affect future engineering work. Current
constraints belong in `MEMORY.md`; accepted architecture decisions belong in ADRs.

## Architectural decisions and design reasoning

### Marketplace and Seller workflow

- The marketplace uses one shared SKU with competing Seller offers (ADR 0006). A Seller
  submits a draft; deduplication proposes an existing or new SKU; a Merchandiser reviews
  it; approval records the assigned SKU and removes the draft. A rejected Seller revises
  and resubmits rather than reusing a Commerce Connect content identifier.
- Each Seller is a Commerce Connect warehouse. Inventory reporting is separate from the
  submission workflow, and Sellers cannot see another Seller's inventory (ADR 0007).
- Native Optimizely content approval was selected instead of a custom review screen or
  placeholder catalog entries. Hidden, versionable submission content enters the Content
  Approvals list; scheduled jobs process terminal outcomes (ADR 0008).
- Durable Service Bus messages bridge the human approval wait and short service outages.
  Submission images use Blob Storage claim checks because they can exceed message limits
  (ADR 0009). The message payload remains deliberately provisional until a real Commerce
  Connect consumer establishes a compatibility contract.

### Seller Portal notifications

- Server-Sent Events were chosen for the Seller Portal BFF because its Next.js proxy
  cannot pass a WebSocket upgrade while retaining the required standalone build mode.
  Notification delivery is intentionally best-effort; the ordinary data endpoints remain
  authoritative. See ADR 0026.
- This does not conflict with DevTools using SignalR: DevTools has a direct endpoint and
  no frontend proxy. The browser topology, not a general preference for either protocol,
  determined the two choices.

### Storefront search and schema migration

- Optimizely Graph was rejected because it requires an unavailable cloud service. The
  default Commerce Connect Lucene provider was rejected because the demo needs an explicit
  search-engine integration. Hot Chocolate was not a substitute because it is an API
  layer, not a search engine. Elasticsearch locally and Azure AI Search when deployed
  were selected under ADR 0022; the Storefront remains isolated behind a search-service
  boundary under ADR 0024.
- Local-only Aspire completion ordering was rejected as a migration-safety mechanism:
  Azure Container Apps cannot provide the same guarantee. The shared migration model uses
  a SQL application lock, schema marker, and readiness checks instead (ADR 0023).
- The Storefront runs Optimizely's shipped SQL scripts, rather than Entity Framework Core
  migrations, through that shared migration model. See ADR 0027.

### Other rejected alternatives

- Separate CMS-only and Commerce Connect sites were rejected because Commerce Connect
  requires CMS and a split would add content federation without a demo benefit (ADR 0001).
- PlantUML replaced Mermaid C4 diagrams after Mermaid layout repeatedly overlapped labels
  and edges. A one-shot Dockerized renderer replaced a long-running PlantUML server that
  returned error images with HTTP success.

## Operational and implementation history

### Identity and authentication hardening

- Keycloak was selected as the local OIDC provider because Aspire can host it without an
  external account. Microsoft Entra External ID was later selected for deployment.
- Storefront OIDC hardening on 2026-09-14 found that Optimizely user synchronization can
  read nullable profile fields as strings. The application now rejects incomplete identity
  claims rather than inventing fallback profile data. Custom claim mapping must be
  registered during service registration, before DI is built.
- A Keycloak restart can invalidate an otherwise unexpired `id_token_hint`. Keycloak-mode
  sign-out therefore confirms tokens with the provider instead of trusting expiration
  alone. A refreshed Keycloak token must persist its rotated refresh token. Concurrent
  requests now coordinate the exchange through Redis, keyed by a one-way token hash and
  scoped to the application. A Redis coordination failure rejects the ticket by product
  decision; this favors strict provider confirmation during that outage.
- The local reverse proxy replaced dynamic localhost browser endpoints. Static Keycloak
  realm configuration now owns browser client settings; the proxy forwards the public
  origin and isolates each application cookie scope (ADR 0025).
- One local Keycloak realm was rejected because it did not isolate the Seller Portal
  identity boundary from Storefront internal identities. ADR 0028 selects separate
  `seller` and `storefront` realms. This intentionally removes local cross-application
  single sign-on; identity brokering was rejected because the required separation did
  not need its added federation behavior.

### Testing lessons

- Isolated tests can hide cross-layer failures when both sides share the same fake or
  serialization assumption. Verify browser, proxy, identity, and transport behavior with
  a minimal real-browser repro before changing a suspected network layer.
- Browser tests exposed JSON casing drift, an incorrect frontend state update, a missing
  logout path, and a hydration race that looked like a database race. Wait for the
  post-hydration state before clicking an interactive server-rendered control.
- Screenshot login must wait until the redirect chain returns to the target origin;
  waiting only for an intermediate document load can race cookie creation and discard the
  requested path.
- A React test that triggers a later `useEffect` must flush the triggering work with
  `act`; otherwise teardown can remove a global stub before the effect mounts.
- A shared AppHost E2E fixture was tried and reverted on 2026-09-15. Fresh browsers did
  not isolate database, cache, identity, messaging, or consumer state. Each E2E test
  therefore owns a complete sequential AppHost and browser until an infrastructure-wide
  reset design proves repeatability.

### Tooling and migration lessons

- The developer CLI replaced the earlier Bash workflow because it needs tested command
  logic and stable agent output (ADR 0016). It currently supports Linux only (ADR 0017).
- Utility-container commands require repository-relative paths: host absolute paths do
  not identify the mounted checkout. The utility image shares a repository `.cache`
  sandbox with local-tool execution.
- AppHost shutdown must wait for the Aspire stop cleanup, rather than treating the stop
  command's return as proof that sibling containers have exited.
- DevTools keeps a bounded in-memory cancellation tombstone because its submission and
  cancellation queues have no shared ordering. Durable reconciliation was rejected for
  development-only tooling; a second restart can still lose that marker.
- CLI async tests synchronize timer and process-start assertions with controllable or
  explicit signals. Wall-clock sleeps are not evidence that asynchronous work happened.
- The reverse-proxy Keycloak resource intentionally has persistent container lifetime but
  no data volume. A data volume would stop realm-import edits from being applied; without
  it, restarts do not retain stale signing keys.

## Archived Historical Summary

- Earlier tool bootstrap, diagram rendering, E2E infrastructure, project-name cleanup,
  Seller Portal migration squashing, package upgrades, and resolved code-review defects
  were removed as completed implementation detail. Git history retains their full record.
- This consolidation retains only decisions, constraints, and incident lessons that remain
  useful for future work.
