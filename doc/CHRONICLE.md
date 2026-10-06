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
  blob reference.

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
  instead of downstream and confusingly. `doc/MEMORY.md` records the current tool
  execution constraints.
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
  the whole Docker-outside-of-Docker/dev-cert/redirect-URI/browser-cert-trust chain;
  later replaced its post-hoc admin-API redirect-URI patch with dynamic OIDC client
  provisioning. A same-process test-parallelism bug (two E2E test classes each starting
  a full Aspire stack concurrently) was fixed with `DisableTestParallelization`. The
  current E2E execution constraint is in `doc/MEMORY.md`.
- **Seller Portal Bff code review** (`ISSUES.md`, now resolved and cleared): fixed a
  public-routing mismatch (`/api/drafts` unreachable through the real `/bff/*` proxy), a
  missing `Database.MigrateAsync()` call, a missing Seller-approval gate,
  `ServiceBusProcessorOptions.AutoCompleteMessages` left at its `true` default alongside
  manual completion, a blob leak on approved-submission cleanup (root-caused fully only
  after two follow-up rounds), an unknown-message-type being silently completed and lost, a
  provisioning race on concurrent first-request Seller creation, and extended the E2E
  test to actually exercise the authenticated path. Two follow-up rounds against the
  *same* blob-leak fix each found a real remaining gap (a publish-vs-commit ordering hole
  in each direction) before the final, durable design was reached.
- **Movie and Merchandise vertical slices built out** in sequence: `eShop.Messaging`
  split out of `eShop.SellerPortal.Bff`/`eShop.DevTools` shared code; `eShop.DevTools`
  added as a Commerce Connect stand-in; the Movie submission E2E test surfaced, and a
  follow-up round root-caused, the frontend
  state-shape bug documented under Testing-strategy lessons; the Merchandise slice
  generalized the provisional `SubmissionRequestMessage` contract to be kind-agnostic (a
  `Kind` enum, nullable movie-only/merchandise-only fields) since it was already
  explicitly provisional (ADR 0009) with no real consumer to keep compatible with yet.
- **Comment-density cleanup**: `AGENTS.md`, `scripts/*.bash` header comments, and
  several `*.cs` files were trimmed to instructions and facts only; historical reasoning
  was consolidated in this chronicle.
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

This file was reduced again on 2026-10-05 at the user's explicit request after the
team captured current operational constraints in `doc/MEMORY.md` and Headroom memory.
The reduction removes duplicated current-state implementation detail from this
chronicle. The chronicle retains decision reasoning, rejected alternatives, testing
lessons, and dated implementation history. `doc/MEMORY.md` is the repository source for
current constraints.
