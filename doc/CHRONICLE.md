# eShop Chronicle

This document is the historical account of how the eShop project reached its current
state: alternatives that were considered and discarded, the reasoning chains behind
accepted decisions, and process incidents. It exists so this reasoning is not lost, but
also does not bloat the documents whose job is to describe the *current* state — the
ADRs under `doc/adr/`, `doc/c4/`, and `doc/MEMORY.md`.

This is a process document, like `doc/TODO.md` and `doc/MEMORY.md`. It is not written in
ASD-STE100 and it is not part of the specification. It is append-only: add to it as new
history happens, and do not delete an entry once written. **Exception**: this file was
compressed on 2026-08-07 and again on 2026-08-12, both at explicit user request, to keep
only knowledge that still affects future engineering work — see "Change summary" at the
end. The append-only convention resumes from this point forward; use git history to
recover the pre-compression text if an older entry's full detail is ever needed.

## Architectural decisions and design reasoning

### Marketplace / Seller Portal workflow

1. **Competing offers**: more than one Seller can offer the same SKU, each with their
   own price (ADR 0006).
2. **Submission workflow**: Seller drafts → submits → automatic deduplication proposes
   an existing SKU match or "new SKU" → Merchandiser reviews.
3. **Merchandiser review powers**: approve the proposed match, pick a different existing
   SKU, mark it as new, or reject with a reason.
4. **Rejection path**: Seller revises and resubmits, restarting from the edit step. This
   also handles two Sellers submitting the same new movie at once — the second review
   simply rejects with a reason to align on the now-existing SKU on resubmission, instead
   of special-casing the race.
5. **Approval outcome**: draft deleted from Seller Portal, assigned SKU saved to the
   Seller's profile.
6. **Inventory** (ADR 0007, amends ADR 0003): each Seller is modeled as a Commerce
   Connect warehouse; inventory is reported per SKU after approval, as a task separate
   from submission, and Sellers cannot see each other's inventory.

### Submission/approval technical design (ADR 0008, ADR 0009)

- Optimizely's native Content Approval system (`IApprovalRepository`/`IApprovalEngine`)
  is reused rather than building a custom review screen. The submission content type
  implements `IVersionable` to plug into it, but is hidden from the CMS/Commerce
  Connect editor tree — Merchandisers review it via the Content Approvals list, not
  tree navigation. This also sidesteps Optimizely's "avoid >100 children per node"
  editor-tree guidance.
- Every submission, including a resubmission after rejection, creates a brand-new
  content item — the Seller Portal never tracks or reuses a Commerce Connect content
  ID, keeping the two apps loosely coupled (ADR 0003).
- Two scheduled jobs clean up terminal states: one processes Rejected submissions
  (queues a rejection message with title/format + reason, then deletes the item); the
  other processes Approved submissions (creates/resolves the product/variant, queues
  the assigned-SKU message, then deletes the item).
- **Messaging (ADR 0009)**: a durable queue, not a synchronous API, is used for both
  integration legs, since the human-approval wait can be long and either app can be
  briefly down. Broker: Azure Service Bus via Aspire's hosting integration (local
  emulator for dev, real namespace for deployment). The real payload shape for both
  messages is deliberately deferred (provisional placeholders only) — there is no real
  Commerce Connect consumer yet to keep compatible with.
- **Claim-check pattern**: Service Bus's message size cap is too small for submission
  images, so images are stored as temporary files in Azure Blob Storage (Azurite
  emulator locally) while a submission awaits review; messages carry only metadata + a
  blob reference. Temporary files are deleted once a submission reaches its final state
  — see the outbox-style ordering invariant below for how that deletion is made safe.

### Rejected alternatives

- **Separate CMS-only and commerce sites** (ADR 0001): needed two CMS instances and
  custom content federation, with no clear benefit for this demo. Rejected for the
  single combined site.
- **Approval applied directly to the live catalog** (ADR 0008): an unmatched submission
  would need a placeholder product/variant created and possibly deleted later. Rejected
  for the separate, hidden submission content type described above.
- **Mermaid's native C4 diagram types**, tried first for `doc/c4/`: auto-layout produced
  overlapping edges and text even after a label-shortening/layout-tuning pass. Switched
  to PlantUML + the C4-PlantUML library (Graphviz-based layout has no such problem),
  rendered via a one-shot Dockerized CLI. An earlier, long-lived `plantuml-server:jetty`
  container (hex-encoded URLs over HTTP) was tried and dropped too: it returned HTTP 200
  with an error-diagram *image* on a syntax error instead of failing, and needed
  encoding/lifecycle bookkeeping the one-shot CLI avoids entirely.
- **SignalR for the Seller Portal Bff's submission push notifications** (an
  approved/rejected outcome shown to a Seller anywhere in the portal): rejected because
  the Next.js reverse proxy in front of the Bff cannot terminate a WebSocket upgrade
  without dropping the `output: "standalone"` build mode the Aspire/Docker pipeline
  needs. Server-Sent Events work through that same proxy unchanged, so the team used SSE
  instead — a single-process, in-memory `SubmissionNotificationBroadcaster` fans a
  submission's outcome out to that Seller's open SSE connection(s), with no replay and no
  cross-instance fan-out: a Seller with zero open connections just misses the push, so
  `GET /bff/api/submissions` stays independently authoritative regardless. **Contrast**:
  `eShop.DevTools`' own later live-update need (ADR 0015) chose SignalR for the same
  *kind* of push, reaching the opposite conclusion — it is registered as a plain Aspire
  project resource with its own direct HTTP endpoint, with no reverse proxy in front of
  it at all, so nothing needs to terminate or proxy a WebSocket upgrade. The proxy
  topology, not the push technology's general merits, is what decides between the two.

### Identity provider (ADR 0002, ADR 0010)

ADR 0002 required one external OIDC provider but left the product open. Keycloak was
picked pragmatically once the AppHost needed something to represent it locally: the
official `Aspire.Hosting.Keycloak` integration runs a real instance with no external
account, matching the local-emulator pattern already used for Service Bus and Blob
Storage. No other product was formally evaluated.

### Storefront search service (ADR 0022)

The Storefront search discussion first considered Optimizely Graph because it replaces
the older Optimizely Search and Navigation product and has strong search features.
Optimizely's docs make Graph a cloud-hosted service for on-prem CMS, with no on-premises
runtime. This project does not currently have access to Optimizely cloud services, so
Graph was rejected for now.

Commerce Connect's default Lucene search provider would have covered the first catalog
search requirements, but it was rejected because the demo should show an explicit search
engine integration.

Hot Chocolate was considered as a way to mimic Optimizely Graph. It was rejected as the
search decision because it provides a GraphQL API layer, not the search index, ranking,
facets, or vector store. It can still sit in front of a search engine later if the
Storefront needs a GraphQL read API.

Azure AI Search became the production choice because it is a managed Azure service with
full-text, filter, facet, vector, and hybrid search support. It also fits the Azure
deployment direction already set by ADR 0018. Elasticsearch became the local
development choice because Azure AI Search has no local emulator, while Elasticsearch
has an Aspire hosting integration and a close enough feature set for local development.

The search service boundary was then split from the backend choice. The Storefront will
call a search microservice rather than linking directly to Elasticsearch or Azure AI
Search client code. This keeps the Storefront insulated from backend differences while
the exact search document shape is still open. The first slice is deliberately only a
basic API plus health checks, so the team can prove hosting and provider configuration
before committing to indexing and query endpoint shapes.

### Database schema migration resources (ADR 0023)

The migration-race discussion first considered a local `WaitForCompletion` dependency
from services to migration resources. That was rejected because Azure Container Apps
has no equivalent completion-ordering primitive for deployed app revisions. Using it
locally would make the local model safer than production and could hide the real
failure mode.

The chosen direction keeps migration ordering useful but not authoritative. Migration
resources still start before dependent services where the orchestrator can do that, but
service correctness comes from database-visible state: a SQL Server application lock, a
schema marker, and readiness checks. This lets local development and production exercise
the same safety model even when their orchestrators differ.

### Utility container image and the local-first/container-fallback pattern

The shared Ubuntu-based utility image (`scripts/Containerfile`) exists because
installing rumdl/PlantUML/shellcheck/etc. natively across every developer's OS was
rejected as too complex; a single Docker image, bind-mounted against the repo, was built
instead (a shape that could also slot into a CI container-job step later). **This
container is still how `eShop.Cli`'s `IContainerRunner` executes the `container`/`auto`
branches of `IToolExecutor` today** — the mechanism below wasn't retired when the bash
scripts were, only re-driven from C# instead of bash.

Constraints baked into that image, several found only by testing directly:

- **Non-root, dynamic UID.** Docker doesn't translate UIDs for bind mounts (unlike
  named volumes) — running as a fixed built-in UID against a host directory owned by a
  different UID fails outright. The image sets a non-root `USER ubuntu` as a sensible
  default, but every container invocation also passes `--user "$(id -u):$(id -g)"`
  (plus `--group-add <docker-socket-gid>` for the Docker-outside-of-Docker case, since
  `--user` alone only sets the primary UID:GID, not supplementary groups) — the dynamic
  match, not the image's baked-in default, is what actually matters. Expected, not
  confirmed, to be a no-op on macOS/Windows Docker Desktop.
- **`--group-add` doesn't work under rootless Docker.** Rootlesskit maps container
  uid/gid 0 to the invoking host user and ids 1-65535 to a subordinate range (see
  `/etc/subuid`/`/etc/subgid`), all inside the daemon's own user namespace. `stat`-ing
  the socket from the plain host reports its group in that subordinate space (e.g.
  `100995`), which is meaningless inside the container's own 0-65535 id space — passing
  it to `--group-add` makes `runc`'s `setgroups()` fail with `EINVAL`
  ("unable to setup user: setgroups: invalid argument"). Detected via `docker info`'s
  `SecurityOptions` containing `rootless` (the same check Docker's own tooling uses);
  when true, `--user 0:0` is used with no `--group-add` at all instead — root inside the
  container is already the invoking host user (via the same uid-0 mapping) and has
  DAC-override within its own namespace, so it can read/write the bind-mounted socket
  regardless of its group.
- **Root-owned build-time artifacts under an arbitrary runtime UID.** The same bug class
  recurred for the .NET SDK, the Aspire CLI, and corepack's pnpm binary: each tool's
  default install location resolves relative to `$HOME`, which the image redirects to a
  path bind-mounted from the repo's own `tmp/` — a location an arbitrary UID can't write
  into (`UnauthorizedAccessException`, not a silent no-op). Fixed by installing each tool
  *outside* that redirected `$HOME`, to an image-baked, `chmod -R a+rwX`-ed path: the
  .NET SDK to `/usr/local/share/dotnet` (with `DOTNET_CLI_HOME` also set — the CLI's own
  home lookup doesn't fully follow `$HOME`), the Aspire CLI to
  `/usr/local/share/aspire-cli/bin` (it writes its own logs/cache two directories above
  its binary, not via `$HOME` or any documented env var), and corepack's pnpm binary to a
  fixed `COREPACK_HOME` (unlike `NUGET_PACKAGES`, a pure re-fetchable cache,
  `COREPACK_HOME` holds the one verified copy of the pinned pnpm binary — resolving it
  under the redirected `$HOME` instead would make corepack silently redownload pnpm from
  the network at container-run time, defeating the point of pinning it).
- **Supply-chain verification, one mechanism per ecosystem.** rumdl/shellcheck/a current
  PlantUML "-mit" build (Ubuntu's own `apt` package was too stale for this repo's
  C4-PlantUML diagrams) are verified via GitHub's per-asset release `digest` field.
  Node's tarball is verified against the `SHASUMS256.txt` file Node publishes per
  release (not a GitHub release asset, so the digest-field mechanism doesn't apply).
  pnpm is installed via corepack rather than `npm install -g pnpm`, specifically so the
  pinned binary itself is hash-verified instead of pulled unpinned from the registry —
  which forced Node's version onto the *Active LTS* line rather than *Current*: corepack
  ships bundled with Node only up to the release where Node's TSC voted to drop it, so
  pinning Current would silently remove corepack. Installing `gh` for its
  attestation-based verification was rejected — it needs a token even for public repos.
- **Version pins follow each ecosystem's own native mechanism**, not an independently
  bumped `ARG`, to avoid drift: the .NET SDK follows `global.json` (via
  `--channel <major.minor> --quality GA`, since `global.json`'s value is a
  `rollForward` anchor, not a real installable version); Node follows the repo-root
  `.nvmrc`; pnpm follows the repo-root `package.json`'s `packageManager` field
  (deliberately at the repo root, not inside any one frontend project, so a second
  frontend project can share the pin — corepack resolves it by walking up parent
  directories from wherever `pnpm` is invoked); the Aspire CLI follows
  `eShop.AppHost.csproj`'s own `Aspire.AppHost.Sdk` version.
- `rumdl`'s only disabled default rule is `MD013` (line length); every other default
  rule is active. `.editorconfig`'s markdown overrides were removed where they
  conflicted with rumdl's own defaults.

The per-tool "local first, container fallback" checks (`command -v dotnet`,
`command -v aspire`, ...) started as an ad hoc bash pattern, added incrementally as each
tool joined the image, then generalized wholesale into `eShop.Cli`'s
`IToolExecutor`/`ExecutionMode.Auto` design described next.

### `eShop.Cli` replaces `scripts/*.bash` (ADR 0016)

Two corrections from the original design:

- **Self-contained publish, not Native AOT.** Native AOT compiles to a native binary for
  the *build machine's* OS only — the Linux-only utility container can't cross-compile a
  macOS/Windows executable that way. Self-contained publish
  (`dotnet publish -r <rid> --self-contained true`) is a NuGet restore + IL packaging
  step, not a native-toolchain step, so it cross-targets fine from any build host.
  Accepted trade-off: a larger, JIT'd-at-first-run footprint — fine for developer-local
  tooling that isn't distributed to end users.
- **`--agent`, a plain boolean, not `--output human|ai`.** Per explicit user direction,
  AI coding agents are instructed via `AGENTS.md` to pass `--agent`. `ESHOP_AGENT` and
  auto-detection via `Console.IsOutputRedirected` remain as fallbacks, mirroring
  `--tools`/`ESHOP_TOOLS`'s existing flag/env-var/auto-detect precedence.

**Design reference** (previously `src/eShop.Cli/SPEC.md`, deleted once its content was
either duplicated here or recoverable from the code itself):

- **Execution strategy.** Every external tool call goes through `IToolExecutor`,
  resolving per invocation: `local` (via `IProcessRunner`), `container` (via
  `IContainerRunner`, the utility image above), or `auto` (default — local if
  `ILocalToolLocator.IsOnPath`, else container). Every tool is switchable under
  `auto`/`local`, including `rumdl`/`shellcheck`/`plantuml` (always containerized in the
  old bash scripts regardless of local availability). One deliberate exception:
  `eshop test e2e`'s `dotnet restore`/`dotnet test` calls always force
  `ExecutionMode.Container` (`ToolInvocation.ForceMode`) — not a "vetted version"
  concern like the rest of the design, but because the suite needs the container's real
  Playwright/Chromium install (ADR 0011), which doesn't exist on a developer's host
  unless separately installed.
- **Output modes.** No command calls `Console.WriteLine`/`AnsiConsole.*` directly — every
  command reports exclusively through `IOutputSink`, resolved once per invocation by
  `GlobalOptionsInterceptor` (`--agent` → `ESHOP_AGENT` → auto-detect).
  `HumanOutputSink` uses rich Spectre.Console widgets (colored status lines with emoji,
  tables, a live spinner). `AiOutputSink` writes plain, ANSI-free, stable-wording text
  with no emoji/spinners/progress ticks, exact-match-tested since AI-mode output
  stability is the highest-value contract to protect (agents parse it).
- **Testing conventions.** `FakeProcessRunner` (records invocations, returns a canned
  result) is the one real process-boundary fake every other test builds on — no mocking
  framework, matching this repo's convention (e.g. `RecordingServiceBusClient`).
  Diagnostics parsing is pure (fixture string in, `IReadOnlyList<Diagnostic>` out).
  `test/eShop.Cli.Tests/Integration/` holds tests needing real Docker/a real local CLI
  tool — `[Trait("Category", "Integration")]`, excluded from `eshop test`'s default run,
  run explicitly with `--filter-trait "Category=Integration"`.
- **Adding a new command**: add `Commands/<Name>Command.cs` (`AsyncCommand<TSettings>`,
  constructor-injected deps) and, if needed, `Commands/<Name>Settings.cs` deriving from
  `GlobalSettings`/`DefaultSettings`; register it on both `Program.cs`'s
  `CommandApp.Configure` tree and the `TypeRegistrar`; route every external process call
  through `IToolExecutor` (use `ToolInvocation.ForceMode` only for a genuine, documented
  hard platform/dependency requirement, like `test e2e`'s); report only through the
  injected `IOutputSink`; add a `FakeToolExecutor`/`FakeProcessRunner`-backed unit test,
  and an `Integration` test only for a genuine real external dependency.
- **Old script → new command mapping**: `restore.bash`→`eshop restore`;
  `build.bash`→`eshop build`; `format.bash`→`eshop format`; `test.bash`→`eshop test`;
  `test-e2e.bash`→`eshop test e2e`; `coverage.bash`→`eshop test coverage`;
  `run.bash`+`internal/run-apphost.bash`→`eshop run`; `render-puml.bash <file>`→
  `eshop diagram render <file>`; `generate-types.bash`→`eshop generate types`.
  `eshop screenshot <url>` has no bash predecessor — it replaces a raw
  `docker run ... chrome --headless ...` line `AGENTS.md` used to document directly,
  now finding the Playwright Chromium binary at runtime instead of hardcoding its
  version-pinned path. There is no `eshop setup`: `setup.bash`'s responsibilities split
  three ways — ensuring Docker/the utility image exist is handled by
  `scripts/eshop.sh`/`.zsh`/`.ps1` before `eShop.Cli` runs; restoring pinned local dotnet
  tools moved into `eshop restore`; trusting the dev-cert and warning about local
  tool-version mismatches now run automatically before every `eshop` command
  (`GlobalOptionsInterceptor`).

The old `scripts/*.bash`/`scripts/internal/` collection was kept side by side with
`eShop.Cli` for a comparison period, at the user's request, then deleted once every
command had a working `eshop` equivalent. `scripts/` now holds only
`eshop.sh`/`.zsh`/`.ps1`, `Containerfile`, and `container.env`.

**Spectre.Console.Cli 0.55.0's public API differs from its own documentation examples**
(confirmed by decompiling the pinned package after the documented approach failed to
compile) — worth knowing before touching CLI wiring again:

- `DefaultTypeRegistrar` is `internal sealed` in this version; use a hand-written
  `TypeRegistrar`/`TypeResolver` pair wrapping `IServiceCollection`/`IServiceProvider`
  instead.
- `IConfigurator<TSettings>.SetDefaultCommand<TCommand>()` returns `void`, not a
  chainable configurator — call it and `SetDescription(...)` as separate statements.
- A command's settings type can't be the abstract `GlobalSettings` directly (Spectre's
  executor registers `(SettingsType, SettingsType)` per command, and an abstract type
  can't be instantiated) — needs a concrete stand-in (`DefaultSettings`).
- A leaf command's settings type must derive from its *containing branch's* declared
  settings type, not just a common ancestor.

**Parallelizing independent steps, a real spinner, and an AI-mode liveness heartbeat.**
`BuildCommand`/`RestoreCommand`/`UnitTestCommand`/`FormatCommand` split into
`Task.WhenAll`-joined branches wherever the underlying tools touch disjoint files/state;
`E2ETestCommand`/`CoverageTestCommand` stayed sequential (real dependencies between
steps); `RunCommand` was excluded (its interactive dashboard/logs would fight a
spinner). Concurrent steps forced result-printing to collect every branch's output and
print all sections once, in the original order, instead of printing as each step
finishes - concurrent incremental printing interleaves unpredictably.

This was the first real use of `IOutputSink.BeginStep`, and surfaced concurrency/
rendering gotchas whose current fix now lives in `HumanOutputSink`/`AiOutputSink`'s own
code comments: blocking inside `BeginStep` deadlocked full test runs under thread-pool
starvation (fixed via a non-blocking `ContinueWith` attachment); a `Panel`'s width
padding is corrupted by raw ANSI bytes in captured subprocess output (fixed by dropping
`Panel` for subprocess output and scoping `NO_COLOR=1` to AI-mode only - a `FORCE_COLOR`
set in the outer shell still overrides this, an accepted gap); and a session-per-step
spinner needs debouncing plus a forced flush before process exit, or a stale frame is
left on screen.

**Linux only, for now (ADR 0017).** `eShop.Cli` shipped with three bootstrap scripts
(`eshop.sh`/`.zsh`/`.ps1`), but only the Linux one was ever actually exercised and
maintained: `AppHostContainerScript`'s container-fallback session script and
`DevCertificateInstaller`'s NSS-database import script both assume a Linux toolchain
(`stat -c`, `certutil`, `pk12util`), and the test suite itself already assumed a Linux
host. Rather than build out real macOS/Windows parity on demand, the team removed
`eshop.zsh`/`eshop.ps1` and the Windows-only `PATHEXT` lookup in `LocalToolLocator`,
making the "supports three operating systems" claim match what's actually true.
Re-adding Windows/macOS support later is a deliberate, tracked future task
(`doc/TODO.md`), not something to quietly patch back in piecemeal.

## System invariants and constraints

- **A production-shipping project must never gain functionality that exists solely to
  recover or instrument a dev-only tool.** `eShop.SellerPortal.Bff` ships to production;
  `eShop.DevTools` does not (it is excluded from publish-mode builds). A DevTools-only
  admin endpoint on the Bff, gated by a pre-shared secret header instead of the real
  `"SellerOnly"` policy, was tried and removed for exactly this reason — see the
  "Domain / business context" correction above. If a dev-only tool needs a recovery
  path, either the tool solves it entirely on its own side, or the capability is
  reframed as a genuine, normal, production-facing feature (as Cancel-review was) that
  happens to also help the dev-only scenario, not a special case bolted onto the
  production project for the dev tool's benefit.
- **A committed Development-only secret (a literal `AddParameter(...)` default in
  `AppHost.cs`, or an `appsettings.Development.json` `Parameters` entry) is not a
  security finding.** The "never commit a secret" practice exists to stop a real,
  exploitable credential from leaking; a value that's inert outside this repo's own
  local Development environment adds no safety by being flagged, only friction — a real
  deployment supplies its own value for every one of these via an environment variable,
  and nothing committed here is ever what a production system actually uses. The line is
  drawn at anything that *could* be replayed against a real system instead (a
  third-party API key, a production connection string, a real user's credential) — see
  `AGENTS.md`'s "Secrets" section for the resulting rule.
- **SQL Server refuses multiple automatic cascade-delete paths; SQLite silently allows
  them.** `Draft.SellerId`/`Submission.SellerId` are `Cascade` from `Seller`;
  `Submission.DraftId` is `SetNull` from `Draft` — deleting a `Seller` could reach
  `Submissions` two ways, which SQL Server's migration rejects outright (`... may cause
  cycles or multiple cascade paths`) but SQLite (what unit tests use via
  `EnsureCreatedAsync`) never catches. `Submission.SellerId` is `Restrict` instead: a
  Submission is a durable business record (an Approved one already survives its own
  Draft's deletion) that should block a Seller delete, not vanish with it — and nothing
  deletes a Seller today anyway. **Any new FK relationship added to this model must be
  checked against SQL Server, not just the SQLite-backed unit tests**, or this bug class
  will resurface invisibly until a real database is involved.
- **A Keycloak realm import's top-level `roles`/`clientScopes` lists are a *replace*, not
  a merge.** Adding an entry there deletes every one of Keycloak's own built-in
  roles/scopes not also listed (this broke every login realm-wide once, by deleting the
  built-in `profile` scope). Anything realm-role- or client-scope-related must be created
  *additively* through the admin API at runtime instead
  (`KeycloakSellerPortalClientProvisioner`), never through `Realms/eshop-realm.json`'s
  static lists.
- **Keycloak matches `redirectUris`/`post.logout.redirect.uris` by exact string,
  including path** — a bare origin does not match a `SignedOutCallbackPath`/
  `signin-oidc` full path. Both are registered as full URLs by
  `KeycloakSellerPortalClientProvisioner`, matching `Program.cs`'s actual OIDC callback
  paths exactly.
- **Keycloak's `VERIFY_PROFILE` required action blocks login until a user has
  `firstName`/`lastName` set** — any seeded test user needs both, or login silently
  fails (invisible to any test that stops before completing a real login).
- **Blob deletion after an approved/rejected Submission is ordered so a partial failure
  is always safely retryable, in both directions.** Publish the per-blob deletion
  message to `seller-submissions-image-deletions` *before* touching the Draft (a failed
  publish leaves the Draft/Images intact for a clean retry of the whole thing), and only
  remove the Draft (via `DraftStatus.PendingImageCleanup`, then
  `SubmissionImageDeletionConsumer` removing the matching `DraftImage`/Draft once the
  blob delete succeeds) *after* that publish is durably committed. Reordering just those
  two calls fixes only one direction's failure window and reopens the other — both
  directions needed the durable "commit before publish" state itself (`Draft`/
  `DraftImage`, no new outbox table) to exist *before* either could act on it.
  `SubmissionImageDeletionConsumer`'s own blob delete is idempotent
  (`DeleteIfExistsAsync`), so a redelivered/duplicated deletion message is harmless.
  `MaxDeliveryCount` on that queue (`3`, set in `AppHost.cs`) governs retry/dead-lettering,
  per ADR 0009's durable-messaging rationale.
- **Any endpoint that writes JSON directly (not via `Results.Ok`/`Results.Json`) must
  explicitly pass the app's configured `IOptions<JsonOptions>` into
  `JsonSerializer.Serialize`**, or it silently uses PascalCase defaults instead of the
  app's camelCase Web defaults — `SubmissionEventsEndpoints.cs`'s raw SSE writes hit
  exactly this, and no existing test could have caught it (see Testing-strategy lessons
  below).
- **Cookies are scoped by domain+path only, never by port** (RFC 6265) — every
  `aspire start`/`eshop run` restart gets new Keycloak/BFF ports, but old
  session/antiforgery cookies from earlier restarts still get sent, and accumulate in a
  long-lived browser session (recreatable as `HTTP 431`, request headers too large, from
  `seller-portal` cookie chunking). The actual restart-survival requirement is a
  persisted ASP.NET Core Data Protection key ring (now backed by Valkey via
  `HybridCache`, see ADR 0012) — without it, a fresh BFF process can't decrypt either
  cookie after a restart. The `seller-portal` auth cookie itself holds only an opaque
  `HybridCache` lookup key, never the ticket — deliberately, after the ticket's own size
  (`SaveTokens = true` stashing Keycloak's raw tokens, never read back anywhere) pushed
  it into ASP.NET Core's cookie chunking. Do not reintroduce `SaveTokens = true` or
  otherwise grow the ticket without reconsidering this.
- **Update to the rule above: `SaveTokens = true` is back, but paired with an
  `OnTicketReceived` handler (`OidcTokenPruning.StripUnusedTokens`) that strips
  `access_token`/`id_token` from the ticket before it's persisted, keeping only
  `refresh_token` (plus `token_type`/`expires_at`).** Nothing in this app reads any of
  these back yet, but a `refresh_token` is the one token a future silent-refresh or
  RP-initiated-logout feature would actually need, and it alone is far smaller than all
  three combined - reconsidered as a deliberate, narrower exception to the rule above,
  not an oversigh.
- **Correction to the entry above: stripping `id_token` broke `/bff/logout`.** The
  entry's own reasoning had it backwards - `/bff/logout` already calls
  `Results.SignOut` against the OpenIdConnect scheme, and `OpenIdConnectHandler`'s
  sign-out flow reads `id_token` back from these same `AuthenticationProperties` to set
  `id_token_hint` on the redirect to Keycloak's end-session endpoint, not
  `refresh_token`. With `id_token` stripped, Keycloak rejected the redirect ("Missing
  parameters: id_token_hint"), caught by an E2E test's logout assertion. Fixed by only
  stripping `access_token`; `id_token` is kept.
- **Follow-up: a kept `id_token` can still go stale enough for Keycloak to reject it as
  `id_token_hint`, distinctly from the missing-parameter bug above.** Keycloak has no
  persistent volume (see "Keycloak runs with no persistent volume" below), so a restart
  wipes its signing keys and session state; the Bff's own auth ticket survives that
  restart (the ticket store is persistent, ADR 0012/0021), including the `id_token` it
  saved at login. Logging out after such a restart sent that now-unverifiable
  `id_token_hint` to the new Keycloak instance, which returned "Invalid parameter:
  id_token_hint" and never redirected back - the Seller's local session was already
  cleared by that point, but the browser dead-ended on Keycloak's own error page with no
  way back. Root-caused by decoding the `id_token_hint` from a live repro URL: its `iss`
  claim pointed at a Keycloak port from a previous container instance. Fixed with
  `OidcSignOutTokenRefresh`, wired into the Cookie scheme's `OnValidatePrincipal`
  (`Program.cs`): every authenticated request checks the saved `id_token`'s expiry and,
  if expired, tries a refresh-token grant against the identity provider's token
  endpoint, persisting the (possibly rotated) tokens back into the ticket on success. If
  the refresh also fails (a fully-forgotten session, not just an expired token), the
  principal is rejected and the Cookie scheme signed out immediately, rather than
  leaving a session only the Bff still believes in.
  **`OnRedirectToIdentityProviderForSignOut` was tried first, then removed as both
  redundant and subtly broken.** Decompiling `OpenIdConnectHandler.HandleSignOutAsync`
  showed it reads `id_token` via `Context.GetTokenAsync(SignOutScheme, "id_token")`,
  which reuses the *same* per-request authenticate result `OnValidatePrincipal` already
  produced - `UseAuthentication()` runs the Cookie handler once per request regardless
  of the endpoint, so by the time `/bff/logout` calls `Results.SignOut`, staleness has
  already been resolved one way or the other. Worse, the redirect-time copy of this
  check had its own bug: when `OnValidatePrincipal` had already rejected the principal,
  `IdTokenHint` arrived as `null`, which its logic treated as "nothing to validate" (not
  "rejected"), silently reproducing the original missing-`id_token_hint` bug instead of
  preventing it. One correct check beats two, especially when the second one is wrong.
- **Follow-up: the fix above still doesn't catch every stale-`id_token` case - confirmed
  by a new E2E test, `SellerPortalStaleIdTokenLogoutTests`, reported failing as expected.**
  `OidcSignOutTokenRefresh.IsExpired` only reads the `id_token`'s own `exp` claim. A
  Keycloak restart happens to also wipe the token's issuer's memory of it, but doesn't
  advance that claim - a still-not-expired `id_token` from before the restart passes
  `ValidateAsync`'s check as `StillValid` without ever contacting Keycloak, and gets sent
  as `id_token_hint` to the restarted instance regardless, reproducing the exact
  "Invalid parameter: id_token_hint" dead end this section's fix was meant to prevent.
  The test forces this deterministically by deleting Keycloak's own embedded H2 database
  (`/opt/keycloak/data/h2`) before `docker restart`-ing the same container - a bare
  restart alone reuses the container's writable filesystem and so preserves the realm's
  keys/sessions/dynamically-provisioned client, silently *not* reproducing the bug (this
  was confirmed the hard way: an earlier version of the test that only ran `docker
  restart` passed). Not yet fixed - `IsExpired` would need to mean "still good enough to
  present to the identity provider," not just "hasn't hit its own timestamp," which likely
  means always validating against the identity provider rather than trusting a local
  claim alone.
- **Fixed the follow-up above, for Keycloak (development) only** - Microsoft Entra
  External ID (production, ADR 0020) is assumed to have stable URLs/keys, so it doesn't
  need this. `OidcSignOutTokenRefresh.ValidateAsync` gained an `alwaysConfirmWithProvider`
  parameter: when true, it skips the `IsExpired` shortcut entirely and always takes the
  existing refresh-token-grant branch, which already correctly returns `Rejected` when
  the identity provider has forgotten the session (proven by the existing
  `ValidateAsync_ReturnsRejected_WhenTheRefreshTokenIsRejected` test). `AuthConfiguration.cs`
  sets it to true for Keycloak, on every authenticated request (not just `/bff/logout`),
  since a fresh `id_token` is also needed elsewhere for upcoming Seller Portal profile
  work - accepting an extra Keycloak round trip and `HybridCacheTicketStore.RenewAsync`
  (Redis write) per request as a development-only cost. Persistent Keycloak data was
  reconsidered as an alternative and rejected again: `KeycloakSellerPortalClientProvisioner.cs`
  unconditionally `POST`s to create the realm role/client-scope/client every run, with no
  get-or-create path, so it would hard-fail with 409 Conflict against a persisted realm
  without a rewrite; it would also still need the client's `redirect_uri` patched every
  run regardless (the Web app's own port isn't pinned either), which is exactly the
  "patch redirect URI after the fact" design already superseded above because of
  concurrent dev-session-vs-E2E-suite Keycloak sharing conflicts.
  **This surfaced a second, distinct bug**: re-running `SellerPortalStaleIdTokenLogoutTests`
  after the fix above still failed, now with Keycloak's "Missing parameters:
  id_token_hint" instead of "Invalid parameter: id_token_hint" - same dead end, different
  cause. `OnValidatePrincipal`'s `Rejected` branch calls
  `SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)` immediately, during
  the authentication middleware phase, before `/bff/logout`'s own handler runs - clearing
  the ticket (and its `id_token`) before `Results.SignOut([Cookie, OpenIdConnect])` could
  read it back as a hint. There is no way to make the OpenIdConnect end-session round
  trip through Keycloak succeed once it has genuinely forgotten the session, regardless
  of what hint is or isn't sent - so `/bff/logout` (`AuthEndpoints.cs`) now checks
  `context.User.Identity?.IsAuthenticated` (reflecting `OnValidatePrincipal`'s outcome for
  this same request) and signs out of the Cookie scheme alone, skipping the doomed
  OpenIdConnect round trip, when the session was already rejected (or never existed).
  With both fixes in place, `SellerPortalStaleIdTokenLogoutTests` passes.
- **Antiforgery failures return `400` with an `X-Antiforgery-Invalid` header, not a plain
  `403`.** A plain 403 is indistinguishable from a genuine "authenticated but not a
  Seller" access-denied response (which other routes already return as 403, for reasons
  a retry can't fix); a stale `XSRF-TOKEN` cookie is retryable (clear it, retry once) and
  must never be confused with the non-retryable case — `bffFetch` reacts to the header,
  not the status code alone. **Accepted gap**: `SubmissionEvents`' raw `EventSource`
  (the native browser API can't see HTTP status on a connection error at all) cannot
  benefit from this distinction; fixing it would need an extra status-probing request for
  a purely silent failure mode.
- **A native (`dotnet`/`aspire` on `PATH`) build/restore and a containerized one are not
  interchangeable state**, in two independent ways — mixing them needs a manual fixup,
  and there is no automatic detection or fix for either (**accepted gap**):
  - NuGet's `obj/project.assets.json` bakes in whichever environment's package folder
    (`packageFolders`) last restored it; a native `dotnet build --no-restore` afterward
    fails with `NETSDK1064` until a plain `dotnet restore` is re-run in that environment.
    `dotnet clean`, a Docker image rebuild, and `dotnet test --no-cache` (not a real
    flag) were all confirmed *not* to fix this.
  - The Aspire SDK bakes `Projects.eShop_AppHost.ProjectPath` (from
    `$(MSBuildProjectDirectory)`) into a generated source file at build time — a build
    done in one environment and run in the other breaks any path resolved relative to it
    (e.g. `.WithRealmImport("./Realms")`). Keep `build`/`run` (`eshop build`/`eshop run`)
    on the same side; re-run the build after switching sides. Separately, `aspire run`'s
    own internal build has been observed to fail (`CS0246`, on files relying on the
    Aspire SDK's implicit `using` injection) only on a truly first-ever build *inside*
    the container — root cause not confirmed; worked around by always running with
    `--no-build` and requiring a prior successful build first, not by fixing the
    underlying cause.
- **A tool-invocation argument that is an absolute *host* path breaks when that tool
  actually executes inside the container** (mounted at `/workspace`, not the host's
  path) — `UnauthorizedAccessException` trying to create a top-level host-rooted
  directory inside the container's filesystem. This exact bug recurred twice in this
  project's history: once for the old bash scripts' `TestResults` directory argument,
  and again for `eShop.Cli`'s `E2ETestCommand`/`UnitTestCommand`/`CoverageTestCommand`
  `--results-directory`/`-targetdir:`/`-reports:` arguments. **Always pass paths
  relative to the repo root/working directory to a tool's own CLI arguments** (it
  resolves correctly under both execution modes, since `WorkingDirectory` is set
  correctly either way) — reserve absolute host paths for code that runs in the CLI's
  own host process afterward (reading a `.trx`/coverage file back, building a `file://`
  link).
- **Every test in `test/eShop.AppHost.E2ETests` starts its own full Aspire stack** (SQL
  Server, Service Bus/Storage emulators, Keycloak) against a real Docker daemon. Running
  two test classes concurrently starves each other's containers (confirmed:
  intermittent health-check failures under xUnit's default cross-class parallelism, gone
  under a clean sequential run). `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
  forces this whole project to run one test at a time. Concurrently running a native
  `eshop run` session and a containerized E2E run has a related, separate problem — see
  "Next.js dev-server build state" below — and detecting that collision has its own
  known blind spot.
- **EF Core misclassifies a newly-`Add()`-ed owned entity with a pre-populated key as
  `Modified`, not `Added`**, unless that key's property is marked `ValueGeneratedNever()`.
  Every `Id` in this app is client-generated (`Guid.CreateVersion7()`), including on
  owned entities (`MovieDraft.Images`, `.FormatVariants`) — EF Core's default heuristic
  infers "already exists" from a non-default key value, silently dropping the insert.
  Fixed in `SellerPortalDbContext`'s model configuration. **Any new owned entity added
  to this model needs the same `ValueGeneratedNever()` call**, or its inserts will
  silently no-op.
- **EF Core SqlServer's built-in transient-error detector doesn't recognize every
  cold-start connection failure.** Confirmed by decompiling
  `SqlServerTransientExceptionDetector.ShouldRetryOn` (pinned
  `Microsoft.EntityFrameworkCore.SqlServer` 10.0.8): it only retries a fixed list of
  `SqlError.Number` values, mostly Windows/WinSock-style codes (e.g. `10054`, `10060`)
  — a "pre-login handshake" failure while `sql`'s container is still starting up (seen
  live: the Bff started and called `MigrateAsync()` before the `sql` container had even
  finished starting, crashing with an unhandled `SqlException`, exit code 134, well
  before `AddSqlServerDbContext`'s default `EnableRetryOnFailure()` — 6 retries — ever
  triggered) isn't guaranteed to match. Because ADR 0018 removed `AppHost.cs`'s
  `.WaitFor(sellerDb)` gating, the startup migration in `Program.cs` now also wraps its
  execution-strategy call in a bounded `RetryOnSqlExceptionAsync` retry that catches any
  `SqlException` directly, independent of EF Core's own transient classification - a
  belt-and-suspenders fix, not a replacement for the execution strategy. Still exhausts
  and fails loudly (not silently) if the database is genuinely unreachable.

## Non-obvious implementation context

### Why some UI bugs need a real screenshot, not just a source-code read

A CSS cascade-layer ordering issue silently zeroing out utility classes, a broken font
variable, or a layout regression are only visible by actually rendering the page. The
bare host usually lacks the shared libraries (`libnspr4`, `libnss3`, `libatk-1.0`, ...) a
real browser needs, so UI-bug investigation (`eshop screenshot`) runs headless Chromium
inside the `eshop-utility` image instead — it already bundles a Playwright-installed
Chromium for `test/eShop.AppHost.E2ETests`.

### Deferring a DI-resolved dependency inside `AddCookie`/`AddDataProtection`'s own fluent setup

`CookieAuthenticationOptions.SessionStore` and `KeyManagementOptions.XmlRepository` both
need a real, DI-resolved instance (`ITicketStore`/`IConnectionMultiplexer`) that doesn't
exist yet at the point `AddCookie`/`AddDataProtection`'s fluent configuration runs,
before the container is built. Both are wired via
`services.AddOptions<TOptions>().Configure<TDep>((options, dep) => ...)` — ASP.NET
Core's own supported pattern for exactly this ordering problem — rather than building a
throwaway `IServiceProvider` or a second, manually-created connection. Both
registrations stay inside the `"Testing"`-environment guard alongside the other
Aspire-client registrations: the antiforgery middleware still exercises Data Protection
under `"Testing"`, but keeps the framework's default ephemeral in-memory repository
there, unchanged from before either persistence feature existed.

### Aspire hosting integration quirks

- **`OnResourceEndpointsAllocatedEvent` shares one serial gate in `DcpExecutor` that
  blocks every executable resource's process start, not just the subscriber's own** —
  unsuitable for a handler that calls out to an external API (confirmed by decompiling
  `Aspire.Hosting`/`Aspire.Hosting.Testing` 13.4.6). `KeycloakSellerPortalClientProvisioner`
  uses `OnResourceReady` instead, which runs off that critical path in its own
  `Task.Run`; `WaitForResourceHealthyAsync` already only resolves once every
  `OnResourceReady` subscriber finishes (rethrowing on fault), giving
  blocking-with-exception-propagation for free — confirmed identical under
  `DistributedApplicationTestingBuilder` (it invokes the AppHost's real entry point via
  reflection into the same inner `DistributedApplication`/`DcpExecutor`, no
  mock/shortcut path).
- **Upgrading a Keycloak endpoint's protocol to https does not rename it from `"http"`
  to `"https"`.** `keycloak.GetEndpoint("https")` fails with "endpoint not allocated";
  the endpoint stays named `"http"` even though its URL is now `https://...`.
- **Keycloak's dev-HTTPS-certificate source, on the Aspire version this repo currently
  pins (13.4.6), is `DeveloperCertificateService` reading the OS/.NET `CurrentUser/My`
  X509 store**, caching key material into `~/.aspire/dev-certs/https/` and feeding
  Keycloak's `KC_HTTPS_CERTIFICATE_FILE`/`KC_HTTPS_KEY_STORE_FILE` env vars from that
  cache — generic, no Keycloak-specific logic. This is unrelated to
  `~/.aspnet/dev-certs/trust/` or to this repo's own `tmp/`-redirected `$HOME`; the
  native `eshop run` path's Keycloak HTTPS endpoint has zero dependency on either. The
  *containerized* path is different: its `$HOME` is redirected into `tmp/home`, so it
  separately needs `dotnet dev-certs https --trust` to have populated *that* context's
  own cache — the recurring `[110] ... error trusting the HTTPS developer certificate`
  warning during setup is expected, harmless noise on Linux (no OS trust store for
  `--trust` to integrate with) and unrelated to whether the cert actually works.
  **Unresolved gap**: nothing currently guarantees a *native* dev cert exists at all on
  a machine that has never generated one.
- **Keycloak runs with no persistent volume** (`.WithDataVolume()`/
  `ContainerLifetime.Persistent` were tried and dropped) — every `aspire start`/
  `eshop run` gets a fully fresh container, and the `seller-portal` OIDC client is
  created dynamically at runtime (`KeycloakSellerPortalClientProvisioner`, via
  `OnResourceReady`, once the Web resource's real port is known) rather than baked into
  the realm import. Accepted cost: Keycloak's boot time is paid on every restart, not
  just the first. This removed a whole class of previously open questions (realm-import
  vs. existing-volume interaction, concurrent E2E-suite-vs-manual-dev-session Keycloak
  sharing) and replaced an earlier, superseded design that patched a redirect URI onto
  the client after the fact via the admin API — rejected once Keycloak's redirect-URI
  matching turned out to have no port-wildcard support at all
  ([keycloak/keycloak#39880](https://github.com/keycloak/keycloak/issues/39880) is still
  open).
- **From a clean NuGet cache, building `eShop.AppHost.csproj` only as a *transitive*
  `ProjectReference`** (e.g. from an E2E test project) **fails to resolve
  `Aspire.AppHost.Sdk`'s own types** — confirmed directly (standalone AppHost build
  first, then the referencing project: works; referencing project alone from clean:
  doesn't). Any new project that references the AppHost transitively needs an explicit
  standalone `dotnet restore src/eShop.AppHost/eShop.AppHost.csproj` before the real
  build/test, not just a restore of the referencing project.
- **Docker-outside-of-Docker networking**: a containerized AppHost run's project
  resources run as plain processes inside whatever process runs them, but *container*
  resources (SQL Server, Keycloak, Service Bus/Storage emulators) run as siblings on the
  *host's* Docker daemon — a container running the AppHost can't reach those siblings'
  ports without `--network host` plus the host's Docker socket bind-mounted (and the
  socket's owning group added via `--group-add`, since `--user` alone only sets the
  primary UID:GID).
- **Headless Chromium doesn't trust the self-signed ASP.NET Core dev cert either** (same
  no-OS-trust-store reason `--trust` only partially succeeds on Linux) — Playwright
  pages need `IgnoreHTTPSErrors = true`, acceptable since it's always a known, local,
  dev-only cert.
- **`Aspire.Hosting.EntityFrameworkCore` 13.4.6-preview.1's `AddEFMigrations` tool
  resource is not usable yet against this project's shape** (tried, then reverted, while
  resolving ADR 0018's `WaitFor` removal): its generated `DotnetToolResource` derives
  `ASPNETCORE_URLS` from the target project's own endpoints assuming an `"https"` one
  exists (the `AddProject<T>` default) — `sellerPortalBff` registers only `"http"`, so
  the template substitution fails outright at startup (`portForServing` can't find a
  service that doesn't exist). Working around that via `configureToolResource` (forcing
  `ASPNETCORE_URLS` to an empty string) got past that failure, but exposed a second,
  unworkaroundable one: the underlying `dotnet ef database update` invocation has no
  retry of its own and reproducibly lost the race against SQL Server's own slow startup,
  twice in a row, even with `.WaitFor(sellerDb)` on the migration resource itself
  satisfied first. Reverted to the migration running inline in the Bff's own startup
  (`Program.cs`), wrapped in the DbContext's own EF Core execution strategy instead - the
  concurrent-multi-replica-migration risk `AddEFMigrations` would have also fixed is
  still open (`doc/TODO.md`). ADR 0023 later chose a custom migration-resource model
  rather than waiting for this package path to mature.
- **ADR 0023 implemented for the Seller Portal database**: a new
  `EShop.SellerPortal.MigrationRunner` project (a plain console AppHost resource, no
  HTTP endpoint at all - sidesteps `AddEFMigrations`' first bug above by construction)
  calls `Database.MigrateAsync()` programmatically, not `dotnet ef database update`, so
  it gets the same retry behavior the Bff's old inline migration had, which that CLI
  command has no equivalent for (the second, unworkaroundable bug above). A new shared
  library, `EShop.SellerPortal.Migrations`, holds `SchemaMigrationLock`
  (`sp_getapplock`/`sp_releaseapplock` - the repo's first raw ADO.NET, since EF Core's
  `ExecuteSql*` has no way to read a stored procedure's return code) and
  `SchemaMarkerStore` (a `SchemaMigrationMarkers` table, one row per component). The
  Bff's `DbContextConfiguration.cs` no longer migrates anything; a new
  `SchemaMarkerHealthCheck` (the repo's first custom `IHealthCheck`) reports the Bff
  not-ready until the marker shows the current migration succeeded, tagged so it affects
  `/health` but not `/alive`.
- **Follow-up: the `EShop.SellerPortal.Migrations` library above, removed.** It had
  shrunk to holding only `SellerPortalMigrationConstants` once `SchemaMigrationLock`/
  `SchemaMarkerStore` were extracted into the shared, technology-agnostic
  `EShop.Migrations.Orchestration` (see its own entry). A dedicated library for two string constants
  was no longer earning its place as a separate project - they moved into
  `EShop.Common`'s `KnownNames` (`MigrationsSellerPortalComponent`/
  `.MigrationsSellerPortalLockName`), and the project/its test project were deleted. A
  migrated component now needs only its runner project, not a runner plus a
  constants-only library.
- **Follow-up: the Storefront's `StorefrontMigrationConstants` (`EShop.Migrations.Optimizely`),
  also moved into `KnownNames`.** Kept there initially since `EShop.Migrations.Optimizely`
  is more than just constants (unlike the now-deleted `EShop.SellerPortal.Migrations`),
  but a component's constants living in the same place regardless of which technology
  migrates it is the more consistent rule - moved to `MigrationsStorefrontCmsComponent`/
  `.MigrationsStorefrontCmsLockName`/`MigrationsStorefrontCommerceComponent`/
  `.MigrationsStorefrontCommerceLockName`, alongside the Seller Portal's own.
- **Follow-up: all 6 migration constants moved again, out of `EShop.Common`'s
  `KnownNames` and into a new `EShop.Migrations.Common` project (`MigrationNames`,
  prefix dropped - implied by the class name now), alongside a second extraction from
  the same review.** `EShop.Common` is referenced by nearly every project in the repo,
  most with nothing to do with migrations - migration naming belongs with the migration
  primitives. Reading `EShop.Migrations.Optimizely` and `EShop.Migrations.Orchestration`
  side by side (they have no duplicated business logic between them - confirmed by an
  `Explore` agent) surfaced a lower-level duplication instead: both hand-rolled the same
  `SqlCommand`-creation-then-execute and nullable-string-column round-trip shapes
  independently (`SchemaMigrationLock`/`SchemaMarkerStore` in one, `OptimizelySqlScriptRunner`
  in the other). `EShop.Migrations.Common` now also holds `SqlCommandExtensions`
  (`ExecuteNonQueryAsync`/`ExecuteReaderAsync`, the latter taking a mapping delegate so
  the command and reader are both disposed inside the extension rather than leaking a
  `SqlCommand` back to the caller) and `SqlNullableValueExtensions`
  (`GetNullableString`/`DbNullIfNull`), and both `EShop.Migrations.Optimizely` and
  `EShop.Migrations.Orchestration` now depend on it. `SchemaMigrationLock.TryAcquireAsync`
  deliberately stayed hand-rolled ADO.NET - it needs the `SqlCommand` alive after
  execution to read back an output `ReturnValue` parameter, which an execute-and-discard
  helper can't support, and generalizing it for that one caller wasn't worth it.
  Behavior-preserving refactor, verified against a real SQL Server (same command text/
  parameters/transactions as before, not just a clean build).
  **`sp_getapplock`'s `LockOwner=Session` ties the lock to one specific SQL Server
  session** - the runner acquires it and runs the migration on the exact same
  already-open `SqlConnection` (`dbContext.Database.GetDbConnection()`), not a second
  connection from EF's own pool, or the lock protects nothing.
  **First live-run attempt crashed anyway, for the exact class of bug this whole change
  exists to prevent**: both the runner's initial `Database.OpenConnectionAsync()` and
  the health check's own connection-open were unwrapped, plain calls - a cold-starting
  SQL Server's pre-login handshake reset (the same failure `SqlExceptionRetry`, moved
  here unchanged from the Bff's old `RetryOnSqlExceptionAsync`, exists to retry) hit
  them before either ever reached that retry logic, crashing the runner outright and
  making the health check throw instead of reporting Unhealthy. Fixed by wrapping the
  runner's own connection-open in the same `SqlExceptionRetry`, and by having the health
  check catch `SqlException` around its connection-open and return a graceful Unhealthy
  result - a health check must never throw, especially not during the exact transient
  window it exists to detect. Verified against a genuinely fresh (never-migrated)
  database via `eshop run`: the runner completes and writes a success marker, the Bff's
  `/health` turns healthy right after, and a second `eshop run` against the
  already-migrated database completes just as cleanly (EF's own migration check finds
  nothing pending). Deployment automation (an Azure Container Apps job gating the Bff's
  rollout) and Storefront extension are still open (`doc/TODO.md`).
- **Follow-up: extending ADR 0023 to the Optimizely CMS and Commerce databases.**
  Optimizely CMS/Commerce Connect has no EF Core migration API, so the Seller Portal's
  `Database.MigrateAsync()` approach doesn't carry over directly. Two alternatives were
  investigated and rejected before landing on a third:
  - **Booting Optimizely's `InitializationEngine` in a headless console host**, to
    trigger its automatic-schema-update pipeline (`DataAccessOptions.UpdateDatabaseSchema`)
    outside a full web app. Optimizely's own docs describe the engine as "invoked when
    `AddCmsHost()` is called on `IServiceCollection`... after DI configuration is
    complete but before any HTTP requests are processed" - suggestive that it doesn't
    strictly need Kestrel, but no documented example of running it in a plain console
    host exists. Rejected as unverified rather than spiked, since a third option worked
    without needing to answer the question at all.
  - **Shelling out to `dotnet-episerver create-cms-database`/`update-database`**.
    Installed the real tool (`EPiServer.Net.Cli` 2.0.0, from Optimizely's own NuGet feed,
    in an isolated scratch directory) and decompiled it with `ilspycmd` to see exactly
    what it does. `CreateDatabaseCms.RunAsync` only creates the database, a SQL login,
    and the ASP.NET Identity tables (`AspNetUsers` etc., via plain EF Core - unrelated to
    the CMS schema) - it never touches `tblContent` or any other CMS table.
    `UpdateDatabase.RunAsync` only applies *incremental* upgrade scripts (walking the
    project's NuGet lock file for every referenced `EPiServer.*` package, resolving the
    NuGet global-packages cache via `dotnet nuget locals global-packages -l`, then
    running each package's `tools/epiupdates*/sql/*.sql` files) - it never runs the
    baseline full-schema script. So the CLI alone cannot provision a fresh database's
    schema at all; the only supported paths for that are the site's own automatic
    startup update, or manually running the baseline script - which is also what the
    docs call the official manual-install method. Rejected anyway even for the
    incremental-only case: it would need the .NET SDK and the tool inside the runner's
    container image, which this repository's migration model already rules out for
    production images (mirroring the EF `AddEFMigrations` CLI-subprocess rejection
    above), and its own retry (`DatabaseHandler.ExecuteWithRetry`, 3 attempts) only
    wraps the connection open, not each script - coarser than what we can do ourselves.
  - **Chosen: run Optimizely's own shipped SQL scripts directly**, reimplementing the
    decompiled `ScriptRunner`/`ScriptValidatorParser`/`SqlStatusCode` algorithm in raw
    ADO.NET (`EShop.Migrations.Optimizely`) rather than shelling out to it. Every
    `EPiServer.*` package's `tools/` folder ships a baseline full-schema script
    (`EPiServer.Cms.Core.sql`, `EPiServer.Commerce.Core.sql`) and incremental scripts
    under `tools/epiupdates/sql` (CMS) and `tools/epiupdates_Commerce/sql`/
    `tools/epiupdates_CMS/sql` (Commerce - the latter folder targets the *CMS*
    connection despite shipping inside the Commerce package, confirmed from the
    decompiled `UpdateDatabase.RunAsync`). Every script opens with a
    `--BEGINVALIDATINGQUERY`/`--ENDVALIDATINGQUERY` block whose query reports whether to
    skip (`AlreadyIn`/0), run (`Valid`/1), or abort (`Invalid`/-1) - confirmed the
    baseline script is just as self-guarding as the incremental ones, so a single
    algorithm (always include the baseline first, then every incremental script still
    ahead of the live version) handles both a fresh install and an upgrade uniformly.
    `EShop.StoreFront.MigrationRunner` reuses `SchemaMigrationLock`/`SchemaMarkerStore`/
    `SqlExceptionRetry` unchanged from the Seller Portal's runner (all three were already
    technology-agnostic, extracted into a shared `EShop.Migrations.Orchestration` project as part of
    this work) - one runner binary, parameterized by a `cms`/`commerce` argument rather
    than duplicated, since only the connection string, script folders, and component/lock
    names differ.
  - **Verified end to end against a real SQL Server** (fresh install applies the
    baseline and writes a success marker; an already-migrated database no-ops per file
    via `AlreadyIn` but still writes a marker; two concurrent runners against the same
    fresh database both succeed, proving the lock does something) using synthetic
    fixture scripts shaped exactly like Optimizely's own (same validating-query block,
    same `GO`-batch layout, a fake version-tracking stored procedure) - not the real
    scripts, since restoring the actual `EPiServer.CMS.Core`/`EPiServer.Commerce.Core`
    packages needs Optimizely's private NuGet feed
    (`https://nuget.optimizely.com/feed/packages.svc/`) added to `NuGet.config`, and
    `AGENTS.md` says not to change that file without being asked. Also still open for
    the same reason: the MSBuild step that would copy those packages' real `tools/`
    scripts into the runner's build output, and scaffolding `EShop.StoreFront.Web` itself
    (needs the `epi-commerce-empty` template from the same feed). See `doc/TODO.md`.
- **Follow-up: the NuGet feed was added, `EShop.StoreFront.Web` scaffolded, and its
  hosting model verified by actually running it - several undocumented compatibility
  issues found, none guessable from Optimizely's own docs.** `nuget.config` gained a
  second source (`https://nuget.optimizely.com/feed/packages.svc/`), scoped via package
  source mapping to `EPiServer`/`EPiServer.*`/`Optimizely.*` only, so every other
  package still resolves from `nuget.org` exactly as before.
  - **Package versions**: `EPiServer.CMS`/`EPiServer.Commerce` (the "meta" packages, not
    the individual `.Core`/`.AspNetCore` sub-packages) at `13.0.2`/`15.1.0` - the NuGet
    package major version really does match the marketing version (CMS 13, Commerce
    Connect 15) once current releases are checked; an earlier assumption that these had
    drifted apart was based on stale packages already sitting in the local NuGet cache
    from an unrelated prior session, not the actual current feed. `EPiServer.CMS` must
    stay at `13.0.2`, not the newer `13.1.1`: `13.1.1` restores against Commerce
    `15.1.0` with *no* NuGet warning, but crashes at runtime - a stale
    assembly-versioned reference inside `Mediachase.Commerce`/`Mediachase.Search`
    (`EPiServer.Events.ChangeNotification, Version=13.0.2.0`) that only exists at the
    13.0.2 line. Two more direct package references were needed purely to satisfy
    runtime assembly loads neither meta-package pulls in transitively -
    `EPiServer.Events.ChangeNotification` (found via the exact `FileNotFoundException`
    above) and `EPiServer.OptimizelyIdentity` (found the same way, one level
    later - `EPiServer.Commerce.UI.Admin.AddCommerceAdmin()` needs it). Neither NuGet
    restore nor `dotnet build` surfaced either gap; only actually running the app did.
  - **`AddCmsAspNetIdentity<ApplicationUser>()` (the `epi-commerce-empty` template's own
    default) is dropped, not adapted.** Its package, `EPiServer.Cms.UI.AspNetIdentity`,
    was never published past `12.34.x` - it doesn't exist for CMS 13 at all, so the
    template's own generated code doesn't compile against current package versions.
    This isn't a loss for this repository regardless: ADR 0002 already makes an
    external OIDC provider the system of record for every identity, so a local
    ASP.NET Identity user store was never wanted here.
  - **Minimal hosting (`WebApplication.CreateBuilder`) does not work with
    `AddCms()`/`AddCommerce()`** - confirmed by actually building and running both
    shapes side by side, not by inference from a plain `IServiceCollection` extension
    method signature (the earlier, wrong assumption). Under minimal hosting,
    `AddCommerce()`'s internal `AddCommerceConnectionString` callback (invoked lazily,
    from `Host.StartAsync`, the first time `IOptions<DataAccessOptions>.Value` is
    requested) throws `InvalidOperationException: No service for type
    'Microsoft.Extensions.Configuration.IConfiguration' has been registered` -
    `IConfiguration` genuinely is registered in the app's real `IServiceProvider` by
    then, and explicitly re-registering it (`builder.Services.AddSingleton(builder
    .Configuration)`) before calling `AddCms()` didn't help, so this is some
    EPiServer-internal service-provider snapshot taken before the callback's closure
    captures a usable one - not a simple registration-ordering fix from the call site.
    The classic model (`Host.CreateDefaultBuilder(args).ConfigureCmsDefaults()
    .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())`) has no
    such problem and reaches all the way to a real database connection attempt.
    `EShop.StoreFront.Web` uses the classic model; every other project in this
    repository keeps using minimal hosting (`EShop.ServiceDefaults` now supports both -
    see the next point).
  - **`EShop.ServiceDefaults/Extensions.cs` was refactored to support both hosting
    models from one implementation**, rather than duplicating its logic into a
    Startup.cs-specific copy. The existing `IHostApplicationBuilder`-generic public
    methods (`AddServiceDefaults<TBuilder>`, `ConfigureOpenTelemetry<TBuilder>`,
    `AddDefaultHealthChecks<TBuilder>`) are unchanged in behavior - every other
    project's call sites needed no changes - but now delegate to new private/public
    helpers taking plain `IServiceCollection`/`IConfiguration`/`ILoggingBuilder`
    directly. Two gaps the classic model doesn't have a single object for, unlike
    `IHostApplicationBuilder`: `ILoggingBuilder` (only reachable via `IHostBuilder
    .ConfigureLogging(...)` at the `Program.cs` level, not from inside `Startup` -
    handled by the new public `ConfigureOpenTelemetryLogging(this ILoggingBuilder)`),
    and mapping health-check endpoints without a combined
    `IApplicationBuilder`/`IEndpointRouteBuilder` object like `WebApplication` (handled
    by splitting `MapDefaultEndpoints(this WebApplication)` into
    `UseDefaultEndpointsMiddleware(this IApplicationBuilder)` (call before
    `UseEndpoints`) and a new `MapDefaultEndpoints(this IEndpointRouteBuilder)` overload
    (call inside it, alongside the project's own endpoint mapping) - one `UseEndpoints`
    call, not two).
  - **Optimizely's own `DatabaseSchemaHost` (a required hosted service, part of
    `AddCms()`) throws and crashes the entire host at startup if the database schema
    doesn't exist yet** (`NotSupportedException: The database schema for 'CMS' is not
    installed`), confirmed by pointing the app at a real, empty SQL Server database
    with `DataAccessOptions.CreateDatabaseSchema = false` set (per ADR 0023, deliberately
    disabled here - see the ISchemaValidator/DataAccessOptions wiring in `Startup.cs`).
    This is a materially different resilience shape than the Seller Portal's: the Bff
    starts up fine on a schema-less database and reports `Unhealthy` via
    `SchemaMarkerHealthCheck` until the marker exists, because EF Core itself never
    validates schema existence at startup. Optimizely's own compatibility-level check
    does, unconditionally, as a hosted service - there's no working equivalent to "start
    anyway, report not ready" available from configuration alone. Readiness gating for
    `EShop.StoreFront.Web` (not yet built) will need to account for this: most likely a
    crash-loop-and-restart story (relying on the orchestrator's own restart policy)
    rather than an in-process `IHealthCheck`, unless a still-unexplored option (e.g. an
    `IHostedService` startup order override, or catching this specific exception) turns
    out to work - still open, not decided.
- **Follow-up: the real Optimizely scripts wired in, the readiness question resolved,
  and `AppHost.cs` wired - all verified against real SQL Server and a real `eshop run`
  session, not just synthetic fixtures.**
  - **The MSBuild copy step exists**: `EShop.StoreFront.MigrationRunner.csproj`'s
    `CopyOptimizelySchemaScripts` target copies `episerver.cms.core`'s and
    `episerver.commerce.core`'s real `tools/` folders into the runner's build output,
    merging `episerver.commerce.core`'s `tools/epiupdates_CMS/sql` into the *Cms*
    destination (confirmed targeting the CMS connection, not Commerce - see the earlier
    entry above). Getting the package version at MSBuild time turned out not to work via
    `@(PackageReference)`'s `%(Version)` metadata under central package management (empty
    at the point this target runs) - `@(PackageVersion)` (the item group
    `Directory.Packages.props` itself populates) works reliably instead. Two real
    `Directory.Packages.props` conflicts surfaced while wiring this, both fixed with
    ADR 0013 security-exception-style direct pins: `EPiServer.CMS.Core`/
    `EPiServer.Commerce.Core`'s declared `Microsoft.Data.SqlClient < 7.0.0` upper bound
    (this repo pins `7.0.1`) - suppressed per-project (`NoWarn=NU1608`), not repo-wide,
    since only the runner and `EShop.StoreFront.Web` need a direct `Microsoft.Data
    .SqlClient` reference at all; and a transitive `System.Security.Cryptography.Xml`
    vulnerability the EPiServer graph pulls in on its own, fixed with a direct pin
    (`10.0.10` - `10.0.9` still tripped the same advisories, unlike the unrelated
    `Microsoft.AspNetCore.DataProtection.StackExchangeRedis` case where `10.0.9` was
    enough). Central package management doesn't override a purely transitive
    dependency's version unless some project directly references it - both the runner
    and its test project needed their own direct `System.Security.Cryptography.Xml`
    reference for the pin to actually take effect.
  - **Verified against real, empty SQL Server databases, for real** (not synthetic
    fixtures): `dotnet run -- cms` and `dotnet run -- commerce` each applied their full
    real schema (80 real CMS tables, `sp_DatabaseVersion` landing at `21001`; 145 real
    Commerce tables, `SchemaVersion` recording the full historical version sequence up
    to `12.2.0`), wrote a success marker, and no-opped cleanly on a second run against
    the now-migrated databases. `test/EShop.StoreFront.MigrationRunner.Tests/
    RealOptimizelyScriptsTests.cs` (a new integration test, with its own copy of the same
    MSBuild copy step) runs the same real scripts in CI, so a future version bump has a
    real test to fail against, not just the synthetic-fixture mechanics test.
  - **The readiness question is resolved**: not `.WaitFor` (rejected - it has no effect
    once deployed to Azure Container Apps, so relying on it would make local behavior
    diverge from production instead of matching it), and not catching
    `DatabaseSchemaHost`'s `NotSupportedException` reactively either. Instead,
    `EShop.StoreFront.Web/Program.cs` checks `SchemaMarkerStore` for both components
    itself (`StorefrontMigrationPreflight.cs`), polling with a 10-second delay up to 30
    attempts, *before* ever building the real CMS/Commerce host - reusing the exact
    marker table both migration runners already write to, rather than adding a new
    mechanism. This is pure in-process C#, so it behaves identically regardless of
    orchestrator (Aspire locally, Azure Container Apps once deployed).
  - **Verified end to end, live**: started `EShop.StoreFront.Web` against two freshly
    created, unmigrated databases - it logged "Waiting for migration ... (attempt
    N/30)..." repeatedly without crashing. Ran both migrations in parallel while it was
    still waiting. The app then logged "Migration for '...' has completed" for both
    components, proceeded to build the real host, and `EPiServer.Data.DatabaseSchemaHost`
    succeeded this time (no crash) - CMS/Commerce initialization completed, real content
    types were created (`BundleContent`, `CatalogContent`, `ProductContent`,
    `RootContent`, etc.), and the app reached "Application started. Press Ctrl+C to shut
    down." - the full chain, proven.
  - **`AppHost.cs` wiring**: the CMS/Commerce SQL databases, both migration runner
    resources (`.WithArgs("cms")`/`.WithArgs("commerce")` - one project, two resource
    instances, matching the "one binary, argument-selected" design), and the Storefront
    web resource all added - no `.WaitFor` on either runner, per the readiness decision
    above. `EPiServer.Data`'s own connection-string convention expects exactly
    `EPiServerDB`/`EcfSqlConnection` (not this repo's usual kebab-case Aspire resource
    names) - bridged explicitly via `.WithEnvironment("ConnectionStrings__EPiServerDB",
    storefrontCmsDb)` rather than `.WithReference`, which would have exposed the
    connection string under the resource's own name instead (`storefront-cms-db`),
    which nothing in `EShop.StoreFront.Web` actually looks up.
  - **Verified inside a real `eshop run` session** - not just the standalone manual runs
    above, but Aspire itself starting and orchestrating every piece via the `AppHost.cs`
    wiring: `storefront-cms-db-migration-runner` and
    `storefront-commerce-db-migration-runner` both reached `Running -> Finished` within
    seconds of the session starting (reusing the same persistent `sql` container the
    Seller Portal's own database lives in), `storefront-web` reached `Running`, and once
    both migrations had finished, its own `/health` and `/alive` endpoints returned `200
    Healthy` - queried directly against the port Aspire assigned it, since the AppHost's
    own log file doesn't capture child-resource stdout (that goes to the dashboard's
    OTLP log stream instead, not inspected here). This is the complete chain, proven with
    the same code path a real `eshop run`/deployed instance would actually take, not a
    hand-assembled approximation of it.
- **Follow-up: `StorefrontMigrationPreflight` only checked `marker.Succeeded`, not the
  marker's version - a real gap, caught by comparing it against
  `SchemaMarkerHealthCheck` (`EShop.SellerPortal.Bff`), which compares against the
  current build's expected EF migration id.** A marker from an older Optimizely version
  (a package bump not yet re-migrated here) would have been wrongly treated as "ready."
  Fixed by comparing `marker.SchemaVersion` against an expected version, matching
  `SchemaMarkerHealthCheck`'s own pattern - but where does "expected version" come from,
  with no EF migration id to ask? The first attempt added a third copy of the pinned
  version number as a literal in `EShop.StoreFront.Web`'s own `appsettings.json`
  (`Directory.Packages.props`'s pin, the migration runner's own `appsettings.json`
  `TargetVersion`, and now this - three places to keep in sync by hand). Replaced
  instead, on the observation that `EShop.StoreFront.Web`/`EShop.StoreFront.MigrationRunner`
  already reference the real `EPiServer.CMS`/`EPiServer.CMS.Core` and
  `EPiServer.Commerce`/`EPiServer.Commerce.Core` packages directly - so the installed
  version doesn't need to be told to the app at all, it can be read from the actual
  referenced assembly. Confirmed empirically (not assumed) that every assembly in one
  Optimizely release train ships the exact same version number as the package itself:
  pinning `EPiServer.CMS` to `13.0.2` resolves `EPiServer.Data`, `EPiServer.Events`,
  `EPiServer.ApplicationModules`, and every other CMS-train package to exactly `13.0.2`
  too (their own NU1608 messages listed dozens, all at that version); `Mediachase
  .Commerce.dll`'s own `AssemblyVersion` is literally `15.1.0.0` for the pinned
  `EPiServer.Commerce.Core` `15.1.0`. `EShop.Migrations.Optimizely`' new
  `OptimizelyInstalledVersion.Get(assemblyName)` (`Assembly.Load(name).GetName()
  .Version`, dropping the always-zero fourth component to match the three-part NuGet
  version string) replaced both the new `ExpectedVersion` config value and the existing
  `TargetVersion` one - one less duplicated literal than before this fix, not one more.
  `EShop.StoreFront.MigrationRunner`'s `appsettings.json` now only has
  `ToolsDirectory`/`BaselineScriptFileName`/`IncrementalFolderNames` left as literals (no
  version at all). Re-verified against a real SQL Server after the change: the runner
  still writes `13.0.2`/`15.1.0` markers correctly, derived from the assembly rather than
  typed by hand.

### Next.js dev-server build state is not shareable across native and containerized runs

Next.js bakes the absolute project root it was built against into `.next`'s manifests
(`required-server-files.json`, `next-server.js.nft.json`) and never invalidates that on
its own — reusing the same `.next` across a native run (a real host path) and a
containerized one (`/workspace`) crashes the dev server in a loop. Fixed by giving each
execution mode its own build-output directory: `next.config.ts` reads `distDir` from a
`NEXT_DIST_DIR` env var, set only for the containerized path — its own generated output
(e.g. `.next-container/**`) must stay in ESLint's ignore list alongside the default
`.next/**`, or the linter treats Turbopack's generated JS as source.

This does not fully solve running a native session and a containerized E2E run
*concurrently*: `AddNextJsApp`'s `WithPnpm()` always runs `pnpm install` before
`pnpm run dev`, and both execution modes share the same `node_modules`/
`pnpm-lock.yaml` (Node's module resolution hardcodes a literal `node_modules` folder at
the project root; pnpm's `virtual-store-dir` relocates only the backing content store,
not that folder) — a concurrent `pnpm install` from one side mutates `node_modules` out
from under the other side's live dev server. A synced-worktree-copy design (rsync via a
`.gitignore`-cascading filter) and a real `git worktree` were both explored and rejected
(duplicates `node_modules`/`obj`/`bin` on disk; a `git worktree` would force
committing/stashing in-progress work before every E2E run). The actual fix is
prevention, not isolation: detect and refuse to start a second AppHost session (native
or containerized) while one is already running, rather than trying to make two live
sessions coexist safely. **Known blind spot**: each execution mode's Aspire CLI state
lives under a different `$HOME`, so a check running in one mode cannot see a session
already running in the other.

**The same baked-in-absolute-path problem resurfaces whenever the Seller Portal Web
project itself moves or is renamed on disk**, not just native-vs-containerized: after
the `eShop.*` → `EShop.*` project rename, the pre-existing `.next`/`.next-container`
directories (gitignored, so a plain directory rename doesn't regenerate them) kept
every cached chunk/source-map/module-resolution entry pointing at the old, now
nonexistent path. The dev server panicked ("Next.js package not found") on every
request while still returning `200`, which looked like a redirect loop in the browser
rather than an outright crash. Fixed by deleting both directories and restarting the
resource - `next dev`/Turbopack regenerates them fresh against the current path with no
other intervention needed. **Any future rename or move of this project's directory
must delete `.next` and `.next-container` first**, the same way switching between
native and containerized execution already required.

### SignalR (ADR 0015) integration-testing snags

- A bare `ServiceCollection().AddSignalR()` (no host/Kestrel) throws resolving
  `IHubContext<THub>` unless `.AddLogging()` is also registered —
  `DefaultHubLifetimeManager<THub>` needs an `ILogger<>` a bare `AddSignalR()` doesn't
  provide on its own. A full ASP.NET Core host already registers logging, so this only
  bites a DI-container-only test.
- SignalR's default WebSockets transport uses `ClientWebSocket` directly, bypassing
  `HttpMessageHandlerFactory` entirely — it cannot be routed through `TestServer`'s
  in-memory handler. A `TestServer`-backed integration test must force
  `HttpTransportType.LongPolling` on the test-side `HubConnection`; the real browser
  client still negotiates WebSockets normally against the real Kestrel endpoint.

### Sonar rule conflict: a custom exception type can satisfy S3871 and still trip CA1515

A custom `Exception` subclass, scoped `internal` to satisfy CA1515 ("this application's
types can be made internal"), then trips S3871 ("exception types should be public") —
and the reverse trips the other rule. Unresolvable for a custom exception in an
*application* (not library) project. Sidestepped by using a plain enum return value
(`Handled`/`UnknownRecord`) instead of throwing at all for what is expected, recoverable
control flow — also a better fit for this codebase's own convention (`SubmissionStatus`,
`DraftStatus` are already plain `internal enum`s).

### `IOutputSink`'s DI chicken-and-egg

`IOutputSink` only resolves correctly after `GlobalOptionsInterceptor.Intercept` has
resolved `GlobalOptions.Output` — which happens before any command's constructor runs
but after Spectre.Console.Cli has already constructed the interceptor itself (and
anything its constructor transitively needs). `GlobalOptionsInterceptor` and
`UtilityImageProvisioner` (built transitively via `IDevCertificateInstaller` →
`IContainerRunner`, for the rare "utility image missing" branch) both get built in that
too-early window and can't take `IOutputSink` directly. Fixed with `Func<IOutputSink>`
wherever eager construction happens before that point, registered once and memoized in
`Program.cs` so the DI-resolved `IOutputSink` and the raw factory always hand back the
same instance — needed since the CLI's final flush, after the command returns, has to
reach whichever instance the command itself used.

### Building the OpenAPI-as-frontend-type-source pipeline (ADR 0014) had three build-time surprises

- **`Microsoft.OpenApi` 2.0.0** (pulled in transitively by `Microsoft.AspNetCore.OpenApi`
  10.0.10) has a known high-severity advisory (`GHSA-v5pm-xwqc-g5wc`, patched at 2.7.5+).
  Central Package Management does not auto-pin a transitive dependency's version — fixed
  with a direct `PackageReference`/`PackageVersion` pin to 2.11.0.
- **Build-time doc generation runs the real `Program.cs` through a mock server**, and
  guarding an Aspire-dependent registration out entirely (rather than substituting a
  non-connecting stand-in) can break an *unrelated* endpoint: Minimal API's
  parameter-binding inference asks `IServiceProviderIsService.IsService(type)` to decide
  whether a parameter is a service or an inferred request body — an unregistered
  `SellerPortalDbContext` makes every `GET` endpoint taking it as a parameter fail with
  `Body was inferred but the method does not allow inferred body parameters`. Fixed by
  registering a syntactically valid but non-connecting stand-in (EF Core's connection and
  the Azure SDK clients' network calls are both lazy, never touched during doc
  generation) instead of omitting the registration.
- **Splitting an `AddAuthentication().AddCookie().AddOpenIdConnect()` chain matters**:
  skipping the whole chain during doc generation (to avoid `AddOpenIdConnect`'s config
  read) breaks `app.UseAuthentication()`, which needs `IAuthenticationSchemeProvider`
  from `AddAuthentication()` regardless of how many schemes are chained onto it. Only the
  trailing `.AddOpenIdConnect(...)` call needs to be conditional.
- Smaller, accepted trade-off: ASP.NET Core's OpenAPI generator emits every `decimal`/
  `int` property as a `number | string` union with a regex `pattern` (standard .NET 10
  behavior, more conservative than the hand-written types it replaced) — fixed at the one
  call site it broke with an explicit `Number(...)` conversion, not by fighting the
  generated type globally.

### `@types/node` is deliberately pinned behind the actual Node runtime

The frontend's `package.json` pins `@types/node` a few major versions behind the
Node runtime version the repo actually targets. This is a deliberate, accepted lag,
not an oversight: the newer `@types/node` majors available at the time introduced
breaking type changes not yet needed by this codebase, and ADR 0013's "every
dependency pinned to one exact version" policy does not require chasing every
major the moment it is published. No `package.json` change was made as part of
recording this - see the pinned version in `package.json` itself, which remains
the source of truth.

### ADR 0013's 40-day quarantine audit surfaced two real vulnerabilities, not just noise

A full audit of every pin in `Directory.Packages.props` against ADR 0013's 40-day rule
found 14 packages, across 5 release trains, published too recently, and downgraded them
to the newest compliant version. Two of those downgrades were wrong and got reverted:
`SQLitePCLRaw.bundle_e_sqlite3` 2.1.11 (the "compliant" version) has a disclosed
high-severity vulnerability (GHSA-2m69-gcr7-jv3q); `Microsoft.AspNetCore.DataProtection.StackExchangeRedis`
10.0.9 transitively resolves `Microsoft.AspNetCore.DataProtection` 10.0.9, which pulls in
`System.Security.Cryptography.Xml` 10.0.9 - five separate disclosed high-severity CVEs.
Both surfaced immediately as build-breaking `NU1903` errors at `dotnet restore`, since
this repo's `TreatWarningsAsErrors` setting (`Directory.Build.props`) applies to NuGet's
own audit warnings too - a real safety net, not something to work around.

The DataProtection vulnerability's exact source was traced precisely, not guessed:
`grep`-ing the restored `obj/project.assets.json`'s dependency graph for which package
declares `System.Security.Cryptography.Xml` pinpointed `Microsoft.AspNetCore.DataProtection`
(itself a transitive dependency, never directly pinned) as the sole path in - meaning
only `Microsoft.AspNetCore.DataProtection.StackExchangeRedis` needed reverting to 10.0.10,
not the whole 10.0.10 release train it happened to share a version number with. Both
reverted pins now carry their own comment invoking ADR 0013's documented exception for a
security fix, kept newer than 40 days on purpose - see the "Non-obvious current
constraints" section of `doc/MEMORY.md`.

## Domain / business context

- Seller approval is a Keycloak realm role (`Seller`) a Site Administrator assigns
  directly in Keycloak — there is no local DB approval field or admin UI. Keycloak is
  dev-only tooling; a real deployment uses a different IdP, so this approval mechanism
  is itself a dev-environment stand-in, not a designed product feature.
- `doc/SPEC.md` sets no minimum image count for a Merchandise draft (unlike a Movie's
  required single cover image) and no drag-reorder for its up-to-3 images — both
  explicit product decisions made when SPEC.md was silent, not implementation
  shortcuts.
- A `Submission` is a durable business record once created: an Approved one already
  survives its own Draft's deletion, which is why it blocks (rather than cascades from)
  a Seller delete — see the FK-cascade invariant above.
- Commerce Connect (the Merchandiser-review counterpart to the Seller Portal's
  submission flow, ADR 0008) has not been scaffolded yet. `eShop.DevTools`' Seller Draft
  Approval Simulator stands in for it locally, with an in-memory pending-submission
  store: once its consumer acks a message, durability transfers from Service Bus to
  that in-memory store, so a simulator restart before a human acts on a row strands the
  corresponding Draft in `PendingReview`. Two recovery mechanisms were tried and removed
  by explicit decision (a "Requeue" button on the simulator; a DevTools-only admin
  endpoint on the Bff gated by a pre-shared secret) — a production-shipping project must
  not gain functionality that exists solely to recover a dev-only tool's lost state (see
  System invariants above). The actual fix is a normal, production-facing Seller
  capability instead: "Cancel review"
  (`POST /bff/api/drafts/{movies|merchandise}/{id}/cancel-review`) lets the Seller
  withdraw a draft from `PendingReview` back to `Draft` at any time, independent of
  whether DevTools/Commerce Connect ever returns a result — the Seller (or a developer
  running a demo) simply cancels and resubmits instead. `eShop.DevTools` reverted to
  plain Approve/Reject once its "Requeue" button became dead code.
- 2026-08-12 clarification: DevTools is not a temporary application to retire after the
  Storefront exists. It remains developer-only tooling for workflow inspection and
  actions that are not suitable for Merchandisers, or cannot be expressed through CMS
  approval sequences. The Storefront replaces DevTools only where real Commerce Connect
  behavior is required.
- Adding "Cancel review" introduced a race: `SubmissionResultConsumer` used to apply
  whatever Approve/Reject result it found to whatever `Submission` row matched by id,
  with no check that the submission was still `Pending`. A late in-flight result for a
  since-cancelled submission would have corrupted a Draft no longer under review. The
  consumer now checks `submission.Status == Pending` before applying anything; anything
  else (including `Cancelled`) is logged and treated as `Handled` - a safe no-op, not
  `UnknownRecord` (which dead-letters, the wrong semantic for an expected race).
  `CancelReviewAsync` publishes a `SubmissionCancelledMessage` after committing the
  cancellation; `eShop.DevTools`' `SellerSubmissionCancellationConsumer` removes the row
  from its store and fires the simulator's SignalR change event, mirroring the existing
  Approve/Reject consumer pattern. `CancelReviewAsync` also broadcasts over the Seller
  Portal's existing per-Seller SSE stream, so a second open tab learns about a
  cancellation live - the portal's toast component deliberately does not also toast on
  this event, since the Seller who clicked Cancel already got a synchronous toast from
  that click.
- The drafts list page and the two per-draft edit pages used to fetch data once on
  mount and never again, so a Merchandiser decision or a second-tab cancellation left a
  stale status showing until the next full reload; separately, "Submit for review" used
  to submit whatever was last *persisted*, not what was on screen. Both are fixed:
  "Submit for review" now auto-saves (a PUT) then submits (a POST) as one user-visible
  action; the submit-readiness gates read live, on-screen state; and all three pages
  keep an `EventSource` open and refetch on *any* event, unfiltered - safe because a
  Seller has at most one Pending submission per draft at a time, and the edit pages'
  form fields are already disabled while a submission is pending, so a refetch can
  never clobber an in-progress edit.
- DevTools' two SignalR hub routes and its Seller-Portal-only types were renamed for
  naming consistency with the `seller-*` Service Bus queue names already in use
  (`SubmissionsHub` -> `SellerSubmissionsHub`, `InventoryHub` -> `SellerInventoryHub`,
  and their backing stores/consumers/folders to match) - a pure naming change, not a
  behavior change. Convention: prefix a top-level identifier with `Seller` only when
  not already qualified by a containing `Seller`-prefixed type; a nested member is left
  unprefixed to avoid stuttering.
- The Submissions and Inventory pages' own submit/report routes did not broadcast over
  SSE (only the async consumer side did), so a second open tab missed a Seller's own
  just-submitted/just-reported action until the next full reload. Fixed by broadcasting
  from both the write route and the consumer, for both Submissions and the newly added
  Inventory SSE channel (`InventoryNotificationBroadcaster`, mirroring
  `SubmissionNotificationBroadcaster`).
- An Approved draft with images is not removed immediately: `SubmissionResultConsumer`
  marks it `PendingImageCleanup` and broadcasts once, then
  `SubmissionImageDeletionConsumer` asynchronously deletes each blob and only removes
  the `Draft` row once none remain, broadcasting the Approved outcome again at that
  point. The edit pages' SSE effect must stay open through `PendingImageCleanup`, not
  just `PendingReview`, to catch that second broadcast and show the "Approved and
  removed" panel on the resulting 404. That second broadcast also caused a duplicate
  toast (`SubmissionEvents` now dedupes on the `id:status` pair — safe, since a
  resubmission always gets a brand new `Submission` id) and was missing from the
  Inventory SSE channel entirely (a fresh approval should make its SKU immediately
  reportable - fixed by also broadcasting an `InventorySummary` for it on approval).

## Testing-strategy lessons

Several real production bugs were invisible to this repo's unit/integration tests
specifically because each layer's own test faked away the exact detail that broke in
reality — only a real, full-stack Playwright E2E run (BFF + real browser + real Next.js
proxy) caught them:

- **JSON casing mismatch** (`SubmissionEventsEndpoints.cs`'s SSE writes came out
  PascalCase, the frontend read camelCase): the BFF's own test serialized and
  deserialized with the same bare defaults on both sides, so the round trip always
  matched; the frontend's test hand-wrote its fake `EventSource` payload in the shape
  the component expects. Neither test could see a mismatch between the two real
  implementations.
- **A frontend state-shape bug that looked like a hung network request**
  (`setDetail(data)` overwriting the whole draft's state with just a mutation endpoint's
  small response DTO, crashing the next render before anything visible showed): looked
  identical, in a screenshot/log trace, to a suspected streaming-proxy hang. Confirmed
  via captured browser-console output, not by re-inspecting the proxy code that turned
  out to be innocent.
- **A logout redirect URI bug** shipped because no test exercised logout at all — only
  login. Extended the existing login E2E test to also click through logout rather than
  starting a second, costly whole-Aspire-stack test just to redo the login steps first.
- **General lesson**: when a bug looks like it must be in transport/networking/a proxy,
  but every layer's own isolated test passes, suspect a shared fixture assumption
  (serialization defaults, a hand-written fake's payload shape) before assuming the
  proxy or transport itself is broken — confirmed directly with a minimal, real-browser
  repro before changing any proxy code, more than once in this project's history.
- **`eshop screenshot --login`'s redirect-chain race**: its Node script clicked the
  Keycloak login button, then only waited for `domcontentloaded` before navigating on
  to the requested URL. That event can resolve on an intermediate step of the
  Keycloak-to-BFF-callback redirect chain, before the BFF's `/bff/signin-oidc` handler
  had actually set the auth cookie. The next navigation raced ahead of authentication,
  the target page's own client-side data fetch got a 401, and the app's own 401
  handler bounced the browser through a second, silent login round trip that always
  lands on the BFF's hardcoded post-login `RedirectUri` (`/drafts`) — discarding the
  originally requested URL with no error, no matter which page had been asked for.
  Every unit test for the script only asserted on string fragments of the generated
  script text, so none of them exercised the actual redirect timing; only a real run
  against a live BFF, browser, and Keycloak surfaced it. Fixed by replacing that
  `waitForLoadState('domcontentloaded')` with `page.waitForURL` gated on "back on our
  own origin, off any `/bff/*` path" — the same general lesson above, restated: a
  same-origin proxy/redirect timing bug can hide behind tests that never drive a real
  browser through the real redirect chain.
- **A React passive-effect mount can outlive its own test.** A Vitest test that clicks
  a button whose async handler eventually flips state to trigger a *new* `useEffect`
  (e.g. opening an `EventSource`) can pass its own assertions and return before React
  actually commits that effect — `findByDisplayValue`'s polling can resolve on an
  earlier render than the one that mounts the effect. The test's `afterEach` then runs
  (`vi.unstubAllGlobals()`, RTL `cleanup()`) before the effect fires, so a global stub
  the effect depends on (a fake `EventSource`) is already gone, throwing
  `ReferenceError` after the test has already reported pass/fail. Wrapping the
  triggering `fireEvent.click` in `await act(async () => { ... })` forces React to
  flush that passive effect before the `act()` call resolves, closing the race.

## Archived Historical Summary

- **Early tooling setup**: rumdl/PlantUML/shellcheck added to the utility image;
  .NET/Node/pnpm/Aspire CLI/`jq` added with the local-first-container-fallback pattern;
  `scripts/restore.bash` split into `setup.bash`+`restore.bash`; every script later made
  to call `setup.bash` itself so a skipped one-time setup step fails fast and clearly
  instead of downstream and confusingly. See "Utility container image" above for the
  constraints that are still true today.
- **Process incidents with no lasting lesson beyond the fix**: `eShop.slnx` briefly
  registered zero projects, so `build.bash`/`test.bash` silently built/tested nothing;
  `render-puml.bash` could overwrite a good `.svg` with a broken one before its own
  exit-code check ran (fixed by rendering to scratch first); a broken `docker ps`/`grep`
  filter during E2E-test cleanup once deleted every running container, including an
  unrelated live AppHost session (recovered via `aspire start`); `shellcheck`'s
  combined-glob run threw one cross-file variable-name false positive, self-resolved
  once the file causing the name collision was later removed; native
  `aspire run`/Ctrl+C and, separately, the containerized fallback both once left
  orphaned `dcp` processes/sibling containers running after a stop — fixed by switching
  to `aspire start`/`aspire stop` (native) and a debounced, `docker ps`-polling stop
  helper (containerized), both later carried into `eShop.Cli`'s
  `AppHostSessionRunner`/single-stop guarantee.
- **Playwright E2E test infrastructure build-out** (ADR 0011): the first test proved out
  the whole Docker-outside-of-Docker/dev-cert/redirect-URI/browser-cert-trust chain (see
  "Aspire hosting integration quirks" above for what's still true); later replaced its
  post-hoc admin-API redirect-URI patch with the dynamic-OIDC-client-provisioner design
  described there. A same-process test-parallelism bug (two E2E test classes each
  starting a full Aspire stack concurrently) was fixed with
  `DisableTestParallelization`, now a documented invariant above.
- **Seller Portal Bff code review** (`ISSUES.md`, now resolved and cleared): fixed a
  public-routing mismatch (`/api/drafts` unreachable through the real `/bff/*` proxy), a
  missing `Database.MigrateAsync()` call, a missing Seller-approval gate,
  `ServiceBusProcessorOptions.AutoCompleteMessages` left at its `true` default alongside
  manual completion, a blob leak on approved-submission cleanup (root-caused fully only
  after two follow-up rounds — see the outbox-style ordering invariant above for the
  final design), an unknown-message-type being silently completed and lost, a
  provisioning race on concurrent first-request Seller creation, and extended the E2E
  test to actually exercise the authenticated path. Two follow-up rounds against the
  *same* blob-leak fix each found a real remaining gap (a publish-vs-commit ordering hole
  in each direction) before the final, durable design was reached.
- **Movie and Merchandise vertical slices built out** in sequence: `eShop.Messaging`
  split out of `eShop.SellerPortal.Bff`/`eShop.DevTools` shared code; `eShop.DevTools`
  added as a Commerce Connect stand-in (see Domain/business context above); the Movie
  submission E2E test surfaced, and a follow-up round root-caused, the frontend
  state-shape bug documented under Testing-strategy lessons; the Merchandise slice
  generalized the provisional `SubmissionRequestMessage` contract to be kind-agnostic (a
  `Kind` enum, nullable movie-only/merchandise-only fields) since it was already
  explicitly provisional (ADR 0009) with no real consumer to keep compatible with yet.
- **Comment-density cleanup**: `AGENTS.md`, `scripts/*.bash` header comments, and
  several `*.cs` files were trimmed to instructions/facts only, with their reasoning
  moved into this file (already merged into the relevant sections above rather than kept
  as a separate log entry).
- **`scripts/internal/` reorganization**: `_fn.bash`/`_run-apphost.bash` moved into a
  new `scripts/internal/` directory and renamed to `lib.bash`/`run-apphost.bash`, purely
  for discoverability (the leading underscore was redundant once the directory itself
  signaled "internal").
- **`eShop.Cli` bootstrap staleness check** initially scanned its own `obj`/`bin` output
  as if it were source, republishing on every single invocation regardless of whether
  anything had actually changed; fixed by excluding `obj`/`bin` from the staleness scan.
- **Minor output-quality fixes alongside the spinner work**: `AppHostSessionRunner`'s
  dashboard-URL banner was silently discarded (the underlying process call wasn't run
  with `Interactive: true`); `CoverageTestCommand` used to dump the entire coverage
  report inline instead of linking the HTML version.
- **Seller Portal migrations squashed to a single `InitialCreate`** (2026-08-12), since
  no production instance has been deployed yet and there is no real data to preserve.
  The six prior migrations (`InitialCreate`, `ValueGeneratedNeverForOwnedIds`,
  `AddDataProtectionKeys`, `AddMerchandiseDraftPrice`, `RemoveDataProtectionKeysTable`,
  `RenameKeycloakSubjectIdToSubjectId`) were deleted and regenerated as one migration
  reflecting the current model; the local SQL Server container's data volume was deleted
  separately to match. `dotnet ef migrations add` scaffolds this project's migration
  files with an incorrectly-cased namespace (`Hj.eShop...` instead of `Hj.EShop...`,
  since no `RootNamespace` MSBuild property overrides the project file's own lowercase
  name) - fix the casing by hand after scaffolding a migration here, or the naming-style
  analyzer fails the build.
- **Follow-up: the RootNamespace casing quirk above, closed.** Every project file and
  folder (`src/eShop.*`, `test/eShop.*`, the `eShop.slnx` solution file itself) was
  renamed to `EShop.*`, so `Directory.Build.props`'s computed
  `RootNamespace = Hj.$(MSBuildProjectName...)` now matches the `Hj.EShop.*` namespace
  every hand-written file already declared. `dotnet ef migrations add` no longer needs
  a manual casing fix afterward. Renamed in lockstep: all `<ProjectReference>` and
  `<InternalsVisibleTo>` entries across every `.csproj`; the Aspire-generated
  `Projects.EShop_*` type references in `AppHost.cs` and the AppHost test projects;
  `eShop.Cli`'s own hardcoded repo-path literals (`RepoPaths.cs`, `RepoRootLocator.cs`);
  and `scripts/eshop.sh`/`scripts/Containerfile`'s references to the CLI and AppHost
  project files. The `eshop` CLI command name, its scripts, and Docker
  image/label names stayed lowercase - those are tool/product names, not .NET project
  names, and were never part of the mismatch.

## Change summary

This file was compressed on 2026-08-07 from ~2,390 lines to roughly a third of that,
at explicit user request, to optimize for future engineering usefulness rather than
historical completeness. Individual incident narratives and bash-script-era
implementation logs with no standing lesson were merged into the "Archived Historical
Summary" above or dropped where fully superseded by later entries; architectural
decisions, system invariants, non-obvious vendor/integration quirks, domain context, and
unresolved risks were kept or restated more concisely, with no facts changed and no new
decisions introduced. See git history for the pre-compression text.

This file was compressed again on 2026-08-12, as part of a repository-wide
documentation-consolidation pass (code comments, `doc/MEMORY.md`, and this file), to
keep only knowledge that still affects future engineering work. The Domain/business
context section's three-entry "Correction to the entry above" chain (a since-removed
DevTools recovery mechanism, superseded by the "Cancel review" Seller capability) and
several bug-fix narratives in the same section were compressed to their surviving
invariants; the `eShop.Cli` "Parallelizing independent steps" subsection was compressed
since its blow-by-blow detail now also lives in `HumanOutputSink`/`AiOutputSink`'s own
code comments. No facts were changed, no new decisions were introduced, and every
invariant, rejected alternative, and vendor/integration quirk referenced by a code
comment elsewhere in the repository was preserved. See git history for the
pre-compression text.
