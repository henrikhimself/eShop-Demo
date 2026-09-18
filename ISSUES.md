# Issues

Last deep review: 2026-09-18

This review followed the 2026-09-18 knowledge-consolidation pass (code comments cleaned
up, `doc/MEMORY.md`/`doc/CHRONICLE.md` compressed). Documentation staleness/duplication
issues that pass already resolved are not re-reported here; areas it flagged for closer
review were prioritized. Every entry from the 2026-08-10 review was re-validated against
current on-disk state.

## Critical

No finding in this review met the Critical bar.

## High

### Concurrent authenticated requests can cause a spurious forced logout via a spent Keycloak refresh token

- **Area:** code
- **Location:** `src/apps/EShop.SellerPortal.Bff/Authentication/AuthConfiguration.cs:68-112`
  (the `OnValidatePrincipal` handler), interacting with
  `src/shared/EShop.TicketStore/OidcSignOutTokenRefresh.cs:33-63`
- **Summary:** `alwaysConfirmWithProvider` is `true` for every non-Entra (Keycloak/dev)
  request, so `OnValidatePrincipal` performs a live `refresh_token` grant call on *every*
  authenticated request, not just when the token is stale. Keycloak rotates the refresh
  token on every use. Two genuinely concurrent authenticated requests from the same
  session (e.g. a page load firing a REST fetch and opening the SSE connection at nearly
  the same time, or two open browser tabs) each read the same not-yet-rotated
  `refresh_token` from `HybridCacheTicketStore`.
- **Failure scenario:** The first concurrent request's refresh succeeds and persists the
  rotated token; the second request's refresh call reuses the now-spent token, Keycloak
  returns `invalid_grant`, and `OnValidatePrincipal` immediately calls
  `RejectPrincipal()` and signs the user out - even though the session was valid a moment
  earlier. Normal SPA usage (initial load + SSE connect, or a second open tab) can
  trigger this.
- **Suggested fix:** Serialize/deduplicate the refresh-token-grant call per ticket key
  (e.g. a per-key semaphore or single-flight cache around the refresh call), or re-check
  the freshly-persisted ticket before treating an `invalid_grant` response as a hard
  rejection.

### Seller Portal Web's `/bff/*` proxy unconditionally trusts client-supplied `X-Forwarded-Host`/`X-Forwarded-Proto` outside the local dev-reverse-proxy topology

- **Area:** code
- **Location:** `src/apps/EShop.SellerPortal.Web/app/bff/[...slug]/route.ts:20-31`;
  `src/EShop.AppHost/AppHost.cs:102,168,172` (`sellerPortalWeb.WithExternalHttpEndpoints()`
  only inside the publish-mode branch, with the whole reverse-proxy resource gated
  behind the non-publish-mode branch); `doc/adr/0025-local-development-reverse-proxy.md:36-37`
- **Summary:** `route.ts` forwards whatever `x-forwarded-host`/`x-forwarded-proto` arrive
  on the incoming request verbatim, correct only because the dev-only reverse proxy
  always overwrites them first (`forwardPublicOrigin: true`). That guarantee comes
  entirely from a resource compiled out in publish mode. In publish mode this same
  Next.js app is exposed directly behind Azure Container Apps ingress, with no
  equivalent component in this repo that sanitizes those headers first. ADR 0025 does
  not document that ACA ingress strips/overwrites a client-sent forwarded-host header.
- **Failure scenario:** In a deployed instance, an external client sends a request
  directly to the public Seller Portal Web origin with a spoofed
  `X-Forwarded-Host`/`X-Forwarded-Proto`. If ACA's ingress passes these through
  unmodified (not verified either way in this repo), `route.ts` forwards them to the
  Bff, which computes OIDC `redirect_uri` and other host-dependent values from them -
  enabling host-header-style spoofing of a production authentication flow.
- **Suggested fix:** Confirm and document that Azure Container Apps' ingress
  strips/overwrites inbound forwarded-host headers before this app sees them (with a
  test/comment recording that as verified platform behavior), or make `route.ts`/the Bff
  independent of that assumption (e.g. validate against an allow-list of known public
  origins per environment).
- **Confidence note:** Medium - the code-level trust gap and dev-vs-publish-mode
  architecture mismatch are fully traced in-repo, but exploitability depends on Azure
  Container Apps' own header-handling behavior, which is not verified anywhere in this
  repository. Reported one severity level down (High, not Critical) per the confidence
  policy.
- **Relationship to prior finding:** This is a previously-undiscovered gap in the
  *production* topology, distinct from the local-dev-topology fix already recorded under
  "Resolved since last review" below (that fix remains correct for local development).

### Cancelling `eshop run` (native path) with redirected, still-open stdin can hang the CLI process indefinitely

- **Area:** code
- **Location:** `src/dev/EShop.Cli/AppHost/ConsoleKeyReader.cs:25-29`
- **Summary:** When `Console.IsInputRedirected` is true, `ReadKeyAsync` calls the
  synchronous, blocking `Console.In.Read()` directly, without observing the cancellation
  token at all. If stdin is redirected from a pipe that stays open with no data (common
  when a harness spawns `eshop run` as a subprocess without redirecting stdin from
  `/dev/null`), this call blocks until data or EOF arrives - cancellation cannot
  interrupt it.
- **Failure scenario:** `Program.cs` wires `Console.CancelKeyPress` to suppress default
  termination and cancel the shared `CancellationTokenSource`. `AppHostSessionRunner`
  awaits `ConsoleKeyReader.ReadKeyAsync(cancellationToken)`; if stdin is redirected but
  not at EOF, that call never returns, the cancellation fallback path is never reached,
  and `aspire stop` is never invoked. The CLI process becomes unresponsive to Ctrl+C and
  must be force-killed, leaving the AppHost and its containers running.
- **Suggested fix:** Wrap the redirected-input read in something cancellation-aware
  (e.g. race a `Task.Run` read against the cancellation token, or use
  `Console.OpenStandardInput().ReadAsync(..., cancellationToken)`). Add a test against
  the real `ConsoleKeyReader` (not just `FakeConsoleKeyReader`) that redirects stdin from
  an open, unclosed pipe and cancels while blocked.

### Storefront's default layout can never be found - `_ViewStart.cshtml` references `_Layout`, the file on disk is `Layout.cshtml`

- **Area:** code
- **Location:** `src/apps/EShop.StoreFront.Web/Views/_ViewStart.cshtml:2`
  (`Layout = "_Layout";`), `src/apps/EShop.StoreFront.Web/Views/Shared/Layout.cshtml`
  (actual filename has no leading underscore)
- **Summary:** `_ViewStart.cshtml` sets the default layout name to `"_Layout"`, but the
  only layout file in the project is `Layout.cshtml` (no underscore). Razor's view
  engine resolves the layout name by file-name convention, and `Layout.cshtml` doesn't
  match.
- **Failure scenario:** Any Razor view rendered via the default `View()` result inherits
  `Layout = "_Layout"` and throws `InvalidOperationException: The layout view
  '_Layout' could not be located` at render time. This hasn't surfaced yet only because
  `FrontPageController.Index()` currently returns `Empty` (no content types exist yet)
  and the E2E suite only ever navigates to `/ui/cms` (the CMS Shell), never a real
  front-end page. The first real page implementation will break immediately.
- **Suggested fix:** Rename `Views/Shared/Layout.cshtml` to
  `Views/Shared/_Layout.cshtml` (or change `_ViewStart.cshtml` to reference `"Layout"`),
  and add a smoke test that actually renders a page through the default layout.

### A missing/misconfigured Optimizely `tools/` scripts folder makes the migration runner report success without creating any schema

- **Area:** code
- **Location:** `src/apps/EShop.StoreFront.MigrationRunner/StorefrontSchemaMigrator.cs:60-90`
  (`MigrateAsync`), `src/migrations/EShop.Migrations.Optimizely/OptimizelyPackageScriptLocator.cs:24-41`
  (`Locate`)
- **Summary:** `OptimizelyPackageScriptLocator.Locate` silently returns a `null`
  `BaselineScript` and an empty `IncrementalScripts` list whenever the configured tools
  directory, baseline file, or incremental subfolders don't exist - it never raises an
  error. `MigrateAsync` builds its file list purely from whatever `Locate` returns; an
  empty list makes `OptimizelySqlScriptRunner.ExecuteAsync` commit an empty, successful
  no-op transaction, after which `MigrateAsync` writes a `Succeeded: true` marker.
- **Failure scenario:** If the tools directory doesn't resolve to a populated folder at
  runtime (a publish/deployment regression, a typo'd config value, or any future
  packaging issue), the runner records a clean success marker even though zero tables
  were created. The migration-readiness preflight then reports the schema ready based on
  that marker, and the real CMS/Commerce host crashes at startup with a schema-missing
  error - but only after the preflight gate already gave a false green light.
- **Suggested fix:** Treat a `null` `BaselineScript` as a hard error in `MigrateAsync`
  (the baseline is expected to always be present), or have `Locate` throw when the tools
  directory doesn't exist at all, rather than returning a result indistinguishable from
  "already fully migrated."

## Medium

### Cross-queue race lets a cancelled submission still show as pending in the Draft Approval Simulator

- **Area:** code
- **Location:** `src/dev/EShop.DevTools/Services/SellerSubmissionCancellationConsumer.cs:40-49`,
  `src/dev/EShop.DevTools/Services/SellerDraftApprovalSimulatorConsumer.cs:35-44`
- **Summary:** `seller-submissions` and `seller-submissions-cancellations` are drained by
  two independent background consumers on two separate queues, with nothing correlating
  delivery order between them. If both messages are still queued when DevTools starts
  (e.g. it was down or restarting while a Seller submitted-then-cancelled), the
  cancellation consumer can process first, find no matching entry, and dead-letter the
  cancellation. The submission then arrives afterward and is added as if still pending.
- **Failure scenario:** DevTools/AppHost restarts (common in this repo) while a
  submit+cancel pair is in flight. The simulator page then shows a submission as pending
  that the Seller already cancelled; a Merchandiser's Approve/Reject click on it appears
  to succeed with no visible effect (the Bff's consumer silently ignores a result for a
  non-`Pending` submission).
- **Suggested fix:** Don't dead-letter an unmatched cancellation immediately (briefly
  retry/re-check, or track cancellations-seen so a later-arriving submission can be
  dropped), or document this as an accepted eventual-consistency gap for this dev-only
  tool.

### `SqlScriptValidatingQueryParser` misclassifies a script with a leading multi-line block comment as having no validating-query block

- **Area:** code
- **Location:** `src/migrations/EShop.Migrations.Optimizely/SqlScriptValidatingQueryParser.cs:29-51`
  (`GetValidationQuery`)
- **Summary:** The leading-comment skip loop only treats a line as "still in a comment"
  if that specific line starts with `--` or `/*`. A conventional multi-line block
  comment with unprefixed continuation lines breaks the loop early and returns `null`,
  which `OptimizelySqlScriptRunner` treats as a missing validating-query block and
  throws.
- **Failure scenario:** Every currently-vendored Optimizely script uses single-line
  comments before `--BEGINVALIDATINGQUERY`, so this doesn't trigger today. A future
  Optimizely package version shipping a script with a multi-line unprefixed header
  comment would fail the migration outright on package upgrade.
- **Suggested fix:** Track "inside an open `/* ... */` block" as explicit parser state
  rather than requiring every line of a multi-line comment to be individually prefixed.

### `SellerPortalStaleIdTokenLogoutTests`'s real-world status is unclear, and `doc/TODO.md`/`doc/CHRONICLE.md` disagree about it

- **Area:** documentation / code
- **Location:** `test/EShop.AppHost.E2ETests/SellerPortalStaleIdTokenLogoutTests.cs:24-56`,
  `doc/TODO.md:124-132`, `doc/CHRONICLE.md`'s "Auth-ticket token-retention saga" (step 4)
- **Summary:** The test method has no assertion of its own after `LogOutAsync`, but the
  shared `E2ETestHarness.LogOutAsync` helper it calls does perform a real Playwright
  assertion (`Expect(page).ToHaveURLAsync(...)`) that fails the test if logout doesn't
  reach the Web origin - so the test is not vacuous, just indirectly asserted.
  `doc/TODO.md` still describes the underlying bug as open with "no passing assertion
  yet," while `doc/CHRONICLE.md` describes an `alwaysConfirmWithProvider` fix as already
  shipped and the test as passing. The fix is confirmed present in
  `AuthConfiguration.cs:80-81`. The test's own inline comment ("Today this fails") was
  not updated after that fix landed (test files are out of scope for the comment-
  consolidation pass). Whether the fix actually covers this test's forced-data-loss
  scenario was not verified by running the test (needs a real Docker/Keycloak stack).
- **Failure scenario:** If the test is actually still failing, it lowers trust in the
  E2E suite's overall signal ("the known Keycloak one"). If it's actually passing, the
  stale comment and `doc/TODO.md` entry mislead a future engineer into re-investigating
  an already-fixed bug.
- **Suggested fix:** Run the test against current code to determine actual status, then
  either update the stale inline comment and `doc/TODO.md`, or mark the test
  `[Fact(Skip = "...")]` with a link to the tracked item if it's still genuinely failing.

## Low

### `eshop screenshot --password` places a plaintext credential in process arguments

- **Area:** code
- **Location:** `src/dev/EShop.Cli/Commands/ScreenshotSettings.cs:52-54`,
  `src/dev/EShop.Cli/Commands/ScreenshotCommand.cs:84`
- **Summary:** `--password` is forwarded as a literal argument into the `docker run ...
  node screenshot-runner.cjs --password <value>` invocation.
- **Failure scenario:** On a shared multi-user development host, another local user can
  read the plaintext credential via `ps aux`/`/proc/<pid>/cmdline` while the command
  runs, or later via shell history.
- **Suggested fix:** Pass the credential via an environment variable or a temp file/stdin
  instead of a CLI argument.

### `EShop.ServiceDefaults/Extensions.cs` cites ADR 0018 for a decision that ADR isn't about

- **Area:** documentation
- **Location:** `src/EShop.ServiceDefaults/Extensions.cs:150`
- **Summary:** The comment `// ADR 0018: harden health endpoints for non-Development
  exposure` sits above request-timeout/output-cache health-check hardening code, but
  ADR 0018 is about choosing Azure Container Apps as the deployment target and never
  mentions health endpoints. No other ADR documents this hardening decision either.
  Independently flagged by two separate reviewers in this pass.
- **Failure scenario:** A future engineer following the ADR reference finds nothing
  relevant, and either re-derives the reasoning from scratch or wrongly concludes the
  code is unmoored from any real decision.
- **Suggested fix:** Correct the ADR number if a different ADR covers this, or drop the
  reference and describe the rationale inline or via `doc/CHRONICLE.md`.

### CLI/output-sink unit tests use fixed millisecond sleeps as a proxy for async ordering

- **Area:** code
- **Location:** `test/EShop.Cli.Tests/Output/AiOutputSinkTests.cs:100-128`,
  `test/EShop.Cli.Tests/Output/HumanOutputSinkTests.cs:73,90`,
  `test/EShop.Cli.Tests/AppHost/AppHostSessionRunnerTests.cs:41-58`
- **Summary:** Several tests assert on state after a hardcoded `Task.Delay` (20-100ms)
  rather than waiting on a signal.
- **Failure scenario:** Under CI/container CPU contention, a scheduling delay can push a
  background task past the fixed window, producing an intermittent, hard-to-repro red
  build unrelated to the code under test.
- **Suggested fix:** Replace fixed delays with a `TaskCompletionSource`/event-based wait
  (as already used correctly elsewhere in the same test suite, e.g.
  `SubmissionEventsEndpointsTests.WaitUntilSubscribedAsync`), with the delay only as a
  timeout backstop.

### Residual duplication in E2E submission-polling logic after the setup-code dedup

- **Area:** maintainability
- **Location:** `test/EShop.AppHost.E2ETests/SellerPortalMovieDraftSubmissionTests.cs:131-143,159-182,207`,
  `test/EShop.AppHost.E2ETests/SellerPortalMerchandiseDraftSubmissionTests.cs:76-87,108-119,135-160,163`
- **Summary:** The "reload devTools page until the pending row appears" loop and the
  "poll `/bff/api/submissions` until Approved" block (plus its private DTO/JsonOptions)
  are still copy-pasted across the two draft-submission test classes, even though the
  broader setup-code duplication this echoes was already fixed via `E2ETestHarness`.
- **Failure scenario:** A change to the submissions DTO shape or polling cadence must be
  updated in multiple places; missing one produces a slow failure only after a full
  Aspire stack + browser run.
- **Suggested fix:** Move both polling helpers (and the shared DTO) into
  `E2ETestHarness` or a small shared helper class.

### `bffFetch`'s antiforgery self-heal retry is untested for image-upload (`FormData`) requests

- **Area:** code / maintainability
- **Location:** `src/apps/EShop.SellerPortal.Web/lib/bff-fetch.ts:67-70`;
  `lib/bff-fetch.test.ts` (only exercises JSON-string bodies)
- **Summary:** The retry-once path re-invokes `bffFetch` with the same `init`, including
  `init.body`. Correct for the two `FormData` call sites today, but unverified by any
  test.
- **Failure scenario:** A future change to image upload (e.g. a raw `ReadableStream`
  body) could silently break the retry-once path for uploads only, with no test to catch
  it.
- **Suggested fix:** Add a test case using a `FormData` body through the
  antiforgery-retry path.

## Resolved since last review

- `eshop test e2e`'s containerized wrapper failed to build `EShop.StoreFront.MigrationRunner`
  (`NuGetPackageRoot` missing its trailing slash) - resolved. The `CopyOptimizelySchemaScripts`
  target (now `src/migrations/EShop.Migrations.Optimizely/CopyOptimizelySchemaScripts.targets`)
  wraps `NuGetPackageRoot` in `$([MSBuild]::EnsureTrailingSlash(...))` before use.
- Cancelling an `EShop.Cli` command left the child process running - resolved.
  `ProcessRunner.cs` registers a cancellation callback that kills the process tree, with
  a passing regression test (`ProcessRunnerTests.RunAsync_Cancelled_KillsTheChildProcess`).
- The container fallback for `eshop run` treated stdin EOF as "stop" - resolved. The
  generated script now only quits on an explicit `q`/`Q` keypress and otherwise blocks
  until `INT`/`TERM`, with a passing regression test.
- Code comments pointed at deleted scripts and a nonexistent "ADR 0016d" - resolved
  across all five previously-cited files; no such references remain.
- E2E test setup was copy-pasted across four test classes - resolved via the shared
  `E2ETestHarness` (bootstrap, health waits, Playwright setup, login/logout all
  centralized). Note: a narrower, separate duplication remains in submission-polling
  logic - see the new Low finding above.
- `X-Forwarded-Host` was trusted from any client - resolved for the local-development
  topology (the dev reverse proxy overwrites the header before the proxy sees it, and
  the proxy correctly rejects a request missing it). See the new High finding above for
  a related, distinct gap in the production/publish-mode topology that this fix does not
  cover.
- Draft image routes leaving dangling blob references after DB failures - re-confirmed
  resolved: every image/draft-delete route commits the DB row removal before publishing
  blob-deletion work.
- Draft update endpoints lacking request validation - re-confirmed resolved via
  `DraftValidation` and the ownership check in `ReconcileFormatVariants`.
- `SubmissionNotificationBroadcaster` could orphan a subscriber - re-confirmed resolved
  (retry-on-`ReferenceEquals` pattern, also used by `InventoryNotificationBroadcaster`).
- The submission/inventory SSE loops accumulated abandoned waiters - re-confirmed
  resolved (one outstanding `WaitToReadAsync` task per connection, both endpoints).
- `SaveTokens = true` contradicted the documented invariant - re-confirmed resolved
  (`OidcTokenPruning` strips only `access_token`; `id_token`/`refresh_token` are kept).
- `PublishSubmissionRequestAsync` caught only `ServiceBusException` - re-confirmed
  resolved (broadened to catch `Exception` generally, excluding caller cancellation).
- (Carried forward from the 2026-08-10 review, unaffected by this pass and not
  re-investigated: `eshop screenshot` capture timing, stale `.trx` files, dropped format
  diagnostics, dangling blob references, draft validation, the arm64 limitation
  documentation gap, `Directory.Build.props` `.sarif` location, `restore.binlog`
  location, `--tools auto` version mismatch handling, the deep-review `ISSUES.md` header
  lint failure, the AppHost-already-running guard, the utility-container rebuild
  staleness check, the Seller inventory slice, the DevTools SKU generator truncation, EF
  migration/model drift checking, native `eshop run` EOF handling, `shellcheck` scope,
  `@types/node` pinning rationale, DevTools in-memory durability, the stale `PLAN-1.md`
  file, Keycloak provisioning `KeyNotFoundException`, `eshop run` exit-code propagation,
  schema-regeneration diagnostics, and the ADR 0011 status pointer.)
