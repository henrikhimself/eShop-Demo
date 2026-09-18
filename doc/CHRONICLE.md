# eShop Chronicle

This document is the historical account of how the eShop project reached its current
state: alternatives that were considered and discarded, the reasoning chains behind
accepted decisions, and process incidents. It exists so this reasoning is not lost, but
also does not bloat the documents whose job is to describe the *current* state — the
ADRs under `doc/adr/`, `doc/c4/`, and `doc/MEMORY.md`.

This is a process document, like `doc/TODO.md` and `doc/MEMORY.md`. It is not written in
ASD-STE100 and it is not part of the specification. It is append-only: add to it as new
history happens, and do not delete an entry once written. **Exception**: this file was
compressed on 2026-08-07, again on 2026-08-12, and again on 2026-09-18, all at explicit
user/consolidation-pass request, to keep only knowledge that still affects future
engineering work — see "Change summary" at the end. The append-only convention resumes
from this point forward; use git history to recover the pre-compression text if an older
entry's full detail is ever needed.

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
  built-in `profile` scope). `Realms/eshop-realm.json` is now the sole source of realm
  data, including client definitions (ADR 0025 removed the runtime admin-API
  provisioners that used to add roles/scopes additively) — anyone editing that file's
  top-level `roles`/`clientScopes` lists directly must still list every built-in
  role/scope they want to keep, not just the new one.
- **Keycloak matches `redirectUris`/`post.logout.redirect.uris` by exact string,
  including path** — a bare origin does not match a `SignedOutCallbackPath`/
  `signin-oidc` full path. Both are hardcoded as full URLs in `Realms/eshop-realm.json`
  for each client, matching `Program.cs`'s actual OIDC callback paths exactly — this only
  works because the reverse proxy (ADR 0025) gives each app one fixed public origin, so
  the realm file no longer needs a port patched in at runtime.
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
  persisted ASP.NET Core Data Protection key ring (now backed by Redis via `HybridCache`,
  ADR 0012/0021) — without it, a fresh BFF process can't decrypt either cookie after a
  restart. The `seller-portal` auth cookie itself holds only an opaque `HybridCache`
  lookup key, never the ticket.
- **Auth-ticket token-retention saga — current state: `SaveTokens = true`, with only
  `access_token` stripped before persisting; `refresh_token` and `id_token` are kept.**
  Each step's reasoning still matters and is easy to get wrong again:
  1. Originally `SaveTokens = true` stashed every raw Keycloak token, unread anywhere,
     which grew the ticket into ASP.NET Core's cookie-chunking territory — fixed by
     turning `SaveTokens` off.
  2. Re-enabled later behind `OidcTokenPruning.StripUnusedTokens` (an `OnTicketReceived`
     handler), stripping both `access_token` and `id_token` and keeping only
     `refresh_token`. This broke `/bff/logout`: `OpenIdConnectHandler`'s sign-out flow
     reads `id_token` (not `refresh_token`) from `AuthenticationProperties` to set
     `id_token_hint` on the redirect to Keycloak's end-session endpoint — Keycloak
     rejected the redirect ("Missing parameters: id_token_hint"), caught by an E2E
     logout assertion. Fixed by stripping only `access_token`.
  3. A kept `id_token` can still go stale enough for Keycloak to reject it as
     `id_token_hint`, distinct from the missing-parameter bug — and not caught by
     checking the token's own `exp` claim, since a Keycloak restart (no persistent
     volume, see below) wipes its signing keys/sessions without advancing that claim, so
     a still-not-expired local `id_token` gets sent to an instance that no longer
     recognizes it ("Invalid parameter: id_token_hint"). Fixed with
     `OidcSignOutTokenRefresh`, wired into the Cookie scheme's `OnValidatePrincipal`:
     every authenticated request checks `id_token` expiry and, if expired, tries a
     refresh-token grant, persisting rotated tokens back on success or signing out
     immediately on failure. An `OnRedirectToIdentityProviderForSignOut` variant was
     tried first and removed — redundant with the per-request `OnValidatePrincipal`
     check (which already runs before `/bff/logout`'s handler), and it had its own bug:
     a rejected principal's `null` `IdTokenHint` was treated as "nothing to validate"
     instead of "rejected," silently reproducing the same dead end.
  4. Checking only the local `exp` claim still wasn't enough — confirmed by an E2E test
     that forces a Keycloak restart with its embedded H2 database deleted (a bare
     `docker restart` alone preserves realm state and doesn't reproduce the bug). Fixed,
     for Keycloak (development) only — Entra External ID (production, ADR 0020) is
     assumed to have stable URLs/keys — with an `alwaysConfirmWithProvider` parameter on
     `OidcSignOutTokenRefresh.ValidateAsync`: when true, it skips the `IsExpired`
     shortcut and always takes the refresh-token-grant branch, which already correctly
     returns `Rejected` once the identity provider has forgotten the session.
     `AuthConfiguration.cs` sets it to true for Keycloak on every authenticated request
     (a fresh `id_token` is also needed elsewhere, for upcoming Seller Portal profile
     work), accepting an extra Keycloak round trip and Redis write per request as a
     development-only cost. This surfaced one more bug: `OnValidatePrincipal`'s
     `Rejected` branch signs the Cookie scheme out immediately, during the
     authentication middleware phase, clearing the ticket's `id_token` before
     `/bff/logout` could read it back as a hint ("Missing parameters: id_token_hint"
     again, different cause). Fixed by having `/bff/logout` check
     `context.User.Identity?.IsAuthenticated` and sign out of the Cookie scheme alone,
     skipping the doomed OpenIdConnect round trip, whenever the session was already
     rejected or never existed.
  **Lesson**: stripping `id_token` breaks logout, and a kept `id_token`'s own `exp`
  claim is not sufficient to know whether the identity provider still recognizes the
  session — that requires actually asking it.
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
  `Aspire.Hosting`/`Aspire.Hosting.Testing` 13.4.6); use `OnResourceReady` instead, which
  runs off that critical path in its own `Task.Run`, with `WaitForResourceHealthyAsync`
  resolving only once every `OnResourceReady` subscriber finishes (rethrowing on fault) —
  blocking-with-exception-propagation for free, confirmed identical under
  `DistributedApplicationTestingBuilder`. (This was learned while building
  `KeycloakSellerPortalClientProvisioner`, since removed — see "Reverse proxy adoption
  complete" below — but the `OnResourceReady` API lesson still applies to any future
  handler with the same shape.)
- **Upgrading a Keycloak endpoint's protocol to https does not rename it from `"http"`
  to `"https"`.** `keycloak.GetEndpoint("https")` fails with "endpoint not allocated";
  the endpoint stays named `"http"` even though its URL is now `https://...`.
- **Keycloak's dev-HTTPS-certificate source, confirmed on Aspire 13.4.6 (not
  reverified since), is `DeveloperCertificateService` reading the OS/.NET
  `CurrentUser/My` X509 store**, caching key material into `~/.aspire/dev-certs/https/`
  and feeding
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
- **Superseded design (kept for the reasoning, not the current mechanism — see "Reverse
  proxy adoption complete" below for what replaced it): Keycloak originally ran with no
  persistent volume and got a fully fresh container on every `aspire start`/`eshop run`,
  with the `seller-portal` OIDC client created dynamically at runtime
  (`KeycloakSellerPortalClientProvisioner`, via `OnResourceReady`, once the Web
  resource's real port was known) rather than baked into the realm import.** This
  replaced an even earlier design that patched a redirect URI onto the client after the
  fact via the admin API — rejected once Keycloak's redirect-URI matching turned out to
  have no port-wildcard support at all
  ([keycloak/keycloak#39880](https://github.com/keycloak/keycloak/issues/39880) is still
  open). Dynamic provisioning was itself later deleted once a fixed reverse-proxy origin
  (ADR 0025) made a static realm-imported client possible again; Keycloak still runs
  with no data volume today, but now with `ContainerLifetime.Persistent`.
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
  resource was tried and reverted** while resolving ADR 0018's `WaitFor` removal: its
  generated `DotnetToolResource` assumes the target project has an `"https"` endpoint
  (`sellerPortalBff` has only `"http"`, so template substitution fails at startup), and
  working around that exposed a second, unworkaroundable gap — the underlying
  `dotnet ef database update` invocation has no retry of its own and reproducibly lost
  the race against SQL Server's own slow startup. Reverted to the migration running
  inline in the Bff's own startup, wrapped in EF Core's execution strategy. ADR 0023
  later chose a custom migration-resource model rather than waiting for this package
  path to mature.
- **ADR 0023 implemented for the Seller Portal database**: `EShop.SellerPortal.MigrationRunner`
  (a plain console AppHost resource, no HTTP endpoint) calls `Database.MigrateAsync()`
  programmatically rather than `dotnet ef database update`, so it gets real retry
  behavior. The lock (`SchemaMigrationLock`, `sp_getapplock`/`sp_releaseapplock` — this
  repo's first raw ADO.NET, since EF Core's `ExecuteSql*` can't read a stored
  procedure's return code) and marker (`SchemaMarkerStore`, a `SchemaMigrationMarkers`
  table) primitives were extracted, after three rounds of consolidation, into a shared,
  technology-agnostic `EShop.Migrations.Orchestration` project, with shared naming
  constants in `EShop.Migrations.Common`'s `MigrationNames` (moved there, not
  `EShop.Common`'s `KnownNames`, since `EShop.Common` is referenced by nearly every
  project regardless of whether it migrates anything). `EShop.Migrations.Common` also
  holds `SqlCommandExtensions`/`SqlNullableValueExtensions`, factored out once the
  Seller Portal and Optimizely runners were found to have independently hand-rolled the
  same `SqlCommand`-creation/execute and nullable-column-read shapes.
  `SchemaMigrationLock.TryAcquireAsync` deliberately stayed hand-rolled ADO.NET — it
  needs the `SqlCommand` alive after execution to read back an output `ReturnValue`
  parameter. **`sp_getapplock`'s `LockOwner=Session` ties the lock to one specific SQL
  Server session** — the runner must acquire it and run the migration on the same
  already-open `SqlConnection`, not a second connection from EF's own pool, or the lock
  protects nothing. The Bff's `DbContextConfiguration.cs` no longer migrates anything;
  `SchemaMarkerHealthCheck` (part of `/health`, not `/alive`) reports not-ready until the
  marker shows the current migration succeeded.
  **First live-run attempt crashed anyway, for the exact class of bug this change exists
  to prevent**: the runner's and health check's own connection-opens were unwrapped
  plain calls, so a cold-starting SQL Server's pre-login handshake reset hit them before
  `SqlExceptionRetry` ever got a chance, crashing the runner and making the health check
  throw instead of reporting Unhealthy. Fixed by wrapping both connection-opens in
  `SqlExceptionRetry` — **a health check must never throw, especially not during the
  exact transient window it exists to detect.**
- **Extending ADR 0023 to Optimizely CMS/Commerce**: Optimizely has no EF Core migration
  API, so the Seller Portal's approach doesn't carry over directly. Two alternatives
  were rejected first: booting Optimizely's `InitializationEngine` in a headless console
  host (no documented example of running it outside Kestrel — rejected as unverified,
  not spiked); and shelling out to `dotnet-episerver create-cms-database`/
  `update-database` (decompiled with `ilspycmd`: it only creates ASP.NET Identity
  tables, never `tblContent`, and only applies *incremental* upgrade scripts, never the
  baseline — it cannot provision a fresh schema at all, and would need the .NET SDK
  inside the runner's container image, which this repo's model already rules out).
  **Chosen**: reimplement Optimizely's own decompiled `ScriptRunner`/
  `ScriptValidatorParser` algorithm in raw ADO.NET (`EShop.Migrations.Optimizely`).
  Every `EPiServer.*` package's `tools/` folder ships a baseline full-schema script plus
  incremental scripts (the Commerce package's `tools/epiupdates_CMS/sql` folder targets
  the *CMS* connection despite shipping inside the Commerce package); every script opens
  with a validating query reporting skip/run/abort, so one algorithm — baseline first,
  then every incremental script still ahead of the live version — handles both a fresh
  install and an upgrade. `EShop.StoreFront.MigrationRunner` is one binary parameterized
  by a `cms`/`commerce` argument, reusing `SchemaMigrationLock`/`SchemaMarkerStore`/
  `SqlExceptionRetry` unchanged.
  - **Package-version gotchas found only by running the app**, not by restore or build:
    `EPiServer.CMS` and `EPiServer.Commerce` (the "meta" packages) must move together as
    one release train — `EPiServer.CMS` `13.1.1` paired with Commerce still on `15.1.0`
    restored with no NuGet warning but crashed at runtime, via a stale
    assembly-versioned reference inside `Mediachase.Commerce`/`Mediachase.Search` that
    only exists at the `13.0.2` line. (Later resolved: see the 2026-09-16 coordinated
    upgrade to `13.1.3`/`15.2.0` in the Archived Historical Summary.) Two extra direct
    package references (`EPiServer.Events.ChangeNotification`, `EPiServer.OptimizelyIdentity`)
    were needed purely to satisfy runtime assembly loads neither meta-package pulls in
    transitively. `AddCmsAspNetIdentity()` (the `epi-commerce-empty` template's own
    default) was dropped, not adapted — its package was never published past `12.34.x`,
    and ADR 0002 already makes external OIDC the identity system of record here anyway.
  - **Minimal hosting (`WebApplication.CreateBuilder`) does not work with
    `AddCms()`/`AddCommerce()`** — confirmed by running both shapes side by side, not by
    inference. Under minimal hosting, `AddCommerce()`'s lazily-invoked connection-string
    callback throws `InvalidOperationException` for `IConfiguration`, even though
    `IConfiguration` genuinely is registered by then — some EPiServer-internal
    service-provider snapshot taken too early, not a registration-ordering fix from the
    call site. `EShop.StoreFront.Web` uses the classic `Host.CreateDefaultBuilder(...)
    .ConfigureCmsDefaults().ConfigureWebHostDefaults(... UseStartup<Startup>())` model
    instead; every other project keeps minimal hosting.
    `EShop.ServiceDefaults/Extensions.cs` was refactored to support both hosting models
    from one implementation — existing generic call sites (`AddServiceDefaults<TBuilder>`
    etc.) are unchanged, but now delegate to helpers taking plain
    `IServiceCollection`/`IConfiguration`/`ILoggingBuilder` directly, covering two gaps
    the classic model lacks a single object for (`ILoggingBuilder`, and mapping
    health-check endpoints without a combined `WebApplication`-like object).
  - **Optimizely's own `DatabaseSchemaHost` throws and crashes the whole host at startup
    if the schema doesn't exist yet** (`NotSupportedException`) — there is no working
    "start anyway, report not ready" option from configuration alone, unlike EF Core.
    Resolved by `StorefrontMigrationPreflight.cs` polling `SchemaMarkerStore` for both
    components itself, before ever building the real CMS/Commerce host (not `.WaitFor`,
    which has no effect once deployed to Azure Container Apps).
  - The MSBuild `CopyOptimizelySchemaScripts` target copies the real packages' `tools/`
    folders into the runner's build output; getting the package version at MSBuild time
    needs `@(PackageVersion)` (the item group `Directory.Packages.props` populates), not
    `@(PackageReference)`'s `%(Version)` metadata (empty under central package
    management at that point). Two ADR 0013 security-exception-style pins were needed
    alongside this: `Microsoft.Data.SqlClient` (EPiServer's `< 7.0.0` upper bound
    suppressed per-project against this repo's `7.0.1`) and a transitive
    `System.Security.Cryptography.Xml` CVE fix — central package management doesn't
    override a purely transitive dependency's version unless some project references it
    directly, so both the runner and its test project needed their own direct reference
    for the pin to take effect.
  - `StorefrontMigrationPreflight` originally checked only `marker.Succeeded`, not the
    marker's schema version — a real gap: a marker from an older Optimizely version (a
    package bump not yet re-migrated) would have been wrongly treated as ready. Fixed by
    comparing `marker.SchemaVersion` against a version read directly off the referenced
    `EPiServer.*` assembly (`OptimizelyInstalledVersion.Get`), confirmed empirically that
    every assembly in one Optimizely release train ships the package's own version
    number — avoiding a third hand-typed copy of the pinned version (alongside
    `Directory.Packages.props` and the runner's own `appsettings.json`).
  - Verified end to end against real SQL Server and a real `eshop run` session: both
    migration runners reach `Running -> Finished`, `EShop.StoreFront.Web` polls and
    waits without crashing, then reaches `/health`/`/alive` `200 Healthy` once both
    finish — the full chain, with real EPiServer packages and content types created, not
    synthetic fixtures. `EPiServer.Data`'s connection-string convention needs exactly
    `EPiServerDB`/`EcfSqlConnection` (not this repo's kebab-case Aspire resource names),
    bridged in `AppHost.cs` via `.WithEnvironment("ConnectionStrings__EPiServerDB", ...)`
    rather than `.WithReference`.

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

### ADR 0013's 7-day quarantine audit surfaced two real vulnerabilities, not just noise

A full audit of every pin in `Directory.Packages.props` against ADR 0013's 7-day rule
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
reverted pins carried their own comment invoking ADR 0013's documented exception for a
security fix. **This exception was later closed**: the 2026-09-16 .NET 10 servicing
package upgrade (Archived Historical Summary) moved both packages onto quarantined
10.0.12 releases; `SQLitePCLRaw.bundle_e_sqlite3` remains the only current ADR 0013
exception — see `doc/MEMORY.md`'s "Non-obvious Current Constraints".

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
- **Cancel review's race condition**: `SubmissionResultConsumer` now checks
  `submission.Status == Pending` before applying an Approve/Reject result — a late
  in-flight result for an already-cancelled submission is logged and treated as
  `Handled` (a safe no-op), not `UnknownRecord` (which dead-letters; the wrong semantic
  for an expected race). Without this check, a late result could corrupt a Draft no
  longer under review.
- All three Seller Portal pages (drafts list, both edit pages) keep an SSE connection
  open and refetch on *any* event, unfiltered — safe because a Seller has at most one
  Pending submission per draft, and the edit pages' fields are disabled while a
  submission is pending, so a refetch can never clobber an in-progress edit. "Submit for
  review" auto-saves (PUT) then submits (POST) as one action, so it always submits
  on-screen state, not last-persisted state. Both the write route and the async consumer
  broadcast over SSE (for Submissions and Inventory alike), so a second open tab sees
  the Seller's own action immediately, not just external decisions.
  An approved draft with images isn't removed until blob cleanup finishes: it sits in
  `PendingImageCleanup`, broadcasting once on approval and again once cleanup completes
  — SSE listeners must stay open through `PendingImageCleanup`, not just `PendingReview`,
  and toasts dedupe on the `id:status` pair (a resubmission always gets a new
  `Submission` id, so this stays safe).
- Naming convention: a DevTools identifier is prefixed `Seller` only when not already
  qualified by a containing `Seller`-prefixed type (matching the `seller-*` Service Bus
  queue names) — a nested member stays unprefixed to avoid stuttering.

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
- **A test click that races React hydration can look like a database race.** Two Seller
  Portal draft-creation E2E failures initially looked like a `SellerProvisioner`
  concurrent-insert race (SQL Server unique-key exceptions were indeed happening and
  recovering correctly), but the actual cause was the test clicking a server-rendered
  button before React hydrated its client event handler. Fixed by waiting for the
  initial empty-drafts state (rendered only post-hydration) before clicking — not by
  adding any database or process lock.

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
- **2026-09-11: Storefront unit-test baseline added.** Added
  `test/EShop.StoreFront.Web.Tests` to the solution and covered
  `Hj.EShop.StoreFront.Web.Foundation.Operations.OperationExtensions` end to end:
  request creation from `HttpContext`/`Controller`, `Ok`/`Fail` response helpers, and
  the `OperationContext` authentication convenience properties. While wiring that test
  project, two dormant compile blockers in the Storefront scaffold had to be removed:
  `FrontPage.cs` still referenced the old `Foundation.Settings` namespace instead of the
  current `Foundation.SiteSettings`, and `Views/Shared/Layout.cshtml` still contained
  unresolved template-only `Cms.*` site-settings types that do not exist in this repo.
  The layout was reduced to a minimal valid shell so the Storefront project can build
  and unit-test again until real Storefront layout/settings blocks are implemented.
- **E2E fixture-sharing experiment (2026-09-15), reverted.** Sharing one sequential
  AppHost across the normal Seller Portal E2E workflows (each test still owning its own
  browser) cut suite time from 4m54s to 2m40s on its first run, but a second run exposed
  cross-test interference — fresh browsers alone don't isolate the AppHost's database,
  cache, identity, messaging, or background-consumer state (a stale logout redirect, a
  lost live-push edit). Reverted: every E2E test again owns and disposes a complete
  AppHost/Playwright driver/browser. The optimization stays deferred until an explicit,
  infrastructure-wide isolation/reset design can prove repeatability.
- **2026-09-16 dependency upgrade wave**, all under ADR 0013's quarantine rule, each
  verified with a full build, the unit-test suite, and the containerized E2E suite: CMS/
  Commerce Connect coordinated to `13.1.3`/`15.2.0` (user-approved ahead of Commerce's
  own quarantine window, specifically to retest the CMS/Commerce version-train mismatch
  above — the retest passed); Aspire to `13.5.3` (`AspireUseCliBundle=true` is required
  from this version on, or `ASPIRE010` fails the build under `TreatWarningsAsErrors`);
  .NET 10 servicing packages to `10.0.12` (closes the `DataProtection.StackExchangeRedis`
  ADR 0013 exception — `SQLitePCLRaw.bundle_e_sqlite3` remains the only current one);
  Microsoft.Extensions resilience/service-discovery/OpenTelemetry, Azure.Messaging.ServiceBus,
  Microsoft.Playwright, and Spectre.Console to their latest quarantine-cleared versions.

## Storefront OIDC/login hardening (2026-09-14)

- **Root cause of a live `/ui/cms` crash**: Optimizely CMS 13.0.2's
  `SynchronizeUsersDB.FindUsersAsync` unconditionally calls `DbDataReader.GetString` on
  `Email`/`GivenName`/`Surname`, but the CMS schema allows all three columns to be null;
  synchronized `admin`/`editor` rows had a null `Surname`. The Storefront's OIDC callback
  added fallback `given_name`/`email` claims but no fallback `family_name`.
- **Fix chosen: strict validation, not another fallback.** Keycloak's Storefront client
  already had complete `profile`/`email` scopes and the `editor` user had complete
  profile data, so inventing a `family_name` fallback would hide an identity-provider
  contract failure instead of surfacing it. `AuthConfiguration`'s OIDC `OnTicketReceived`
  now requires non-empty `preferred_username`/`email`/`given_name`/`family_name` claims
  before calling Optimizely synchronization; a missing/whitespace claim fails
  authentication (naming the missing claim) and never synthesizes profile data. Unit
  tests cover each missing/whitespace claim; the Storefront browser test now visits
  `/ui/cms` after login so this failure class is caught going forward.
- **`ClaimTypeOptions`'s custom claim-name mapping must be registered during service
  registration, before DI is built** — registering it from the deferred OpenID Connect
  options callback instead was tried and was a real defect (Optimizely kept default
  claim names and synchronized null profile columns even though Keycloak supplied the
  mapped claims).
- **`eshop screenshot --login` generalized**: it was hard-coded to the Seller Portal's
  `/bff/login` route, unusable for the Storefront's `/ui/cms`-triggered OIDC challenge
  (no `/login` endpoint there). It now takes a caller-provided, same-origin
  `--login-path` plus `--username`/`--password`, and waits for the redirect chain to
  return to the target origin — avoiding a CLI resource registry that would duplicate
  application-owned routing. Screenshot storage-state keys off a SHA-256 of (origin,
  login path, username), since cookies don't scope by port and more than one
  authenticated resource/user now exists.
- **Seller Portal OIDC callback origin bug found via the generalized screenshot login**:
  the Keycloak client registration used the BFF's direct URL, but the Next.js proxy
  carries the browser-facing OIDC callback on its own origin — contradicting the
  intended topology (the frontend owns OIDC redirects/the session cookie). Fixed by
  registering the Seller Portal Keycloak client from the `seller-portal-web` resource's
  allocated URL instead of the BFF's.

## Reverse proxy adoption complete (ADR 0025, 2026-09-18)

- Dynamic Keycloak OIDC client provisioning (`KeycloakSellerPortalClientProvisioner`,
  `KeycloakStorefrontClientProvisioner`, `KeycloakAdminApiClient`, their tests, and the
  already-deactivated `OnResourceReady` hooks) was deleted once a real E2E login/logout
  round trip passed through the reverse-proxy hosts using only the static realm import.
- Keycloak got `ContainerLifetime.Persistent` with deliberately no `WithDataVolume()` —
  closes the common trigger where an ordinary restart minted a fresh container/signing
  keys under an already-valid Bff auth ticket (logout failure via Keycloak's
  `Invalid parameter: id_token_hint`), without freezing `eshop-realm.json` edits (a data
  volume would stop `--import-realm` from ever re-importing again). See `doc/TODO.md`
  for the narrower remaining trigger (an actual data loss, not just a restart).
- New E2E coverage: `KeycloakDiscoveryTests`, `CrossAppSsoAuthorizationTests` (a
  Storefront SSO session must not grant Seller Portal `SellerOnly` access), and
  `ForwardedOriginTrustTests` (a spoofed `X-Forwarded-Host`/`-Proto` cannot move the OIDC
  `redirect_uri` off the real origin).
- **Non-obvious build gotcha**: `CopyOptimizelySchemaScripts.targets` concatenated
  `$(NuGetPackageRoot)` directly with a package folder name; the container's
  `NUGET_PACKAGES` env var has no trailing slash (unlike a native restore's default), so
  the two merged into one broken path string — undetected because `eshop test e2e` had
  not run in-container since the target was added. Fixed with
  `$([MSBuild]::EnsureTrailingSlash(...))`.

## PLAN-1.md upstream reverse-proxy contribution (in progress)

- `lib/DotNet-ReverseProxy` submodule branch `feature/aspire-13-5-gateway-support`
  (based on upstream `develop`) upgrades that project's own Aspire hosting/example
  AppHost to `13.5.3` (also needing `AspireUseCliBundle=true` for `ASPIRE010`) and
  implements the approved opt-in forwarded-origin mode: a required `forwardPublicOrigin`
  argument that strips and replaces client `X-Forwarded-For`/`-Host`/`-Proto`, sends no
  client IP, and preserves no original public Host origin — verified against real HTTPS
  targets receiving the correct host/`:8443` port regardless of hostile forwarded
  headers.
- StyleCop's location-less `SA1516` warnings from generated Aspire reference source were
  left unsuppressed by deliberate choice, not fixed.
- No local commits are pushed until `PLAN-1.md` is complete and the user has finished
  code review — see `PLAN-1.md` itself for remaining work (upstream PR, merge, NuGet
  publication, eShop release-history, automated forwarding tests, docs).

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

This file was compressed a third time on 2026-09-18, as part of a repository-wide
documentation-consolidation pass (Phase 3, following code-comment consolidation and a
`doc/MEMORY.md` compression). The auth-ticket token-retention saga and the ADR
0023-to-Optimizely migration saga (both under "System invariants and constraints"/
"Non-obvious implementation context") were compressed to their current correct state
plus the reasoning chain that led there, without dropping any still-relevant lesson;
routine package-upgrade logs, a reverted E2E-fixture experiment, and several bug-fix
narratives with no lasting lesson beyond their fix were merged into "Archived Historical
Summary" or folded into "Testing-strategy lessons"; four same-day 2026-09-14 entries
about Storefront OIDC/login hardening were merged into one section, and the two
2026-09-18 PLAN-1.md/PLAN-2.md status entries were compressed to their decision-relevant
content. No facts were changed, no new decisions were introduced, and every invariant,
rejected alternative, and unresolved risk referenced elsewhere in the repository was
preserved. See git history for the pre-compression text.
