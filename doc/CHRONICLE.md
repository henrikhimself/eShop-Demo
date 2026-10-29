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

### Dependency vulnerability remediation

- On 2026-10-15, the team approved an ADR 0013 security exception for the two-day-old
  10.0.13 cryptography package releases. Direct package references apply the patched
  Pkcs and Xml packages only in projects whose resolved graphs otherwise contain an
  affected version. Central transitive pinning was rejected because it surfaced unrelated
  dependency-downgrade errors across the solution.

### Certificate initialization hang and cancellation correction

- The apparent hang after CLI publishing was in the certificate prerequisite that runs
  before `env`, not in publishing or the AppHost keyboard menu. Repeating
  `certutil -N --empty-password` against an initialized NSS database enters NSS
  password-change logic. Without a controlling terminal, it repeats
  `Error opening input terminal for read` and `Invalid password.  Try again.` The utility image and
  a disposable copy of the affected database both reproduced this behavior.
- The proposed terminal guard around the AppHost key menu was discarded. Original
  Bash input handling did not produce the NSS messages. The guard preserved terminal
  keys but unnecessarily disabled piped key input and could not affect `env`.
- Certificate setup now initializes only a missing NSS database and supplies explicit
  password sources. An exported PFX is not a completion marker: failed imports and a
  removed shared home remain retryable. Exports are staged until successful; command
  failures are reported; the completion marker is written only after trust succeeds.
- Startup checks previously received an uncancellable token while the CLI's Ctrl-C
  handler suppressed immediate exit. They now receive the command token, which lets
  container cancellation reach the existing independent container-removal path.
  Certificate diagnostics retain a bounded tail per stream; other tools retain full
  output because build and test diagnostics depend on it.
- Verification used fresh and existing NSS databases, repeated setup, an invalid PFX
  followed by retry, and a password-protected database. The invalid inputs failed
  without a password loop or a false completion marker. Ctrl-C during blocked setup
  returned exit code 130 and removed the utility container.
- The independent .NET combined trust check reported a nickname mismatch for the
  PFX-imported NSS entry, not missing browser trust. NSS server validation succeeded,
  and real Chromium loaded HTTPS with `ignoreHTTPSErrors=false`. No trust-store
  nickname migration was added to this correction.
- All 213 CLI tests passed in the utility container without a Docker socket mount.
  Native execution exposed the pre-existing socket-fixture issue tracked in
  `TODO.md`. Full repository restore and build also passed after reducing .NET
  concurrency with `DOTNET_PROCESSOR_COUNT=2` and disabling MSBuild node reuse for
  verification. This devbox had a two-GiB cgroup memory limit despite host-level
  `free` output showing more RAM; cgroup counters recorded OOM kills during the
  initial unrestricted attempt.

### Shell script line endings and `ShellScriptBuilder`

- After the certificate correction, `eshop.sh env` failed on a Debian workstation with
  `sh: 1: set: Illegal option -`. The import script was a C# raw string literal, which
  keeps the source file's line endings. The working tree was CRLF, so `set -eu\r`
  reached dash. The index was LF; `.gitattributes` has no `eol` rule for `.cs` files.
- Shell scripts in the CLI are now composed with `ShellScriptBuilder`, which ends every
  line with `\n` regardless of source or platform line endings. `AppendFormat` was
  discarded: `string.Format` treats shell braces as placeholders, and `Append($"...")`
  covers every need. `AppHostContainerScript` also used `AppendLine`, which emits the
  platform newline, and now uses the builder too.
- The installer tests wrote fake `certutil` and `pk12util` scripts from raw literals and
  failed for the same reason in a CRLF tree; the test helper now normalizes line endings.
- A `.gitattributes` rule such as `*.cs text eol=lf` was not added; it is a separate
  repository decision.
- `eshop.sh build` runs `dotnet format`, which may change files pulled from other
  developers. Such changes are not part of this correction.
