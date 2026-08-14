# Issues

Last deep review: 2026-08-10

Existing entries from the 2026-08-08 review were rechecked against the current
repository state. Most of them are now resolved by the PLAN-1 implementation work.
Only the findings below remain open or were newly found during this review.

## Critical

No finding in this review met the Critical bar.

## High

### `eshop test e2e`'s containerized wrapper fails to build `EShop.StoreFront.MigrationRunner` - `NuGetPackageRoot` missing its trailing slash

- **Area:** build/CI infrastructure

- **Location:** `src/EShop.StoreFront.MigrationRunner/EShop.StoreFront.MigrationRunner.csproj:58-105`
  (the `CopyOptimizelySchemaScripts` target), specifically lines 69-70:

  ```xml
  <_CmsCoreToolsDir>$(NuGetPackageRoot)episerver.cms.core\@(_CmsCoreReference->'%(Version)')\tools\</_CmsCoreToolsDir>
  <_CommerceCoreToolsDir>$(NuGetPackageRoot)episerver.commerce.core\@(_CommerceCoreReference->'%(Version)')\tools\</_CommerceCoreToolsDir>
  ```

  and `scripts/Containerfile:160`:

  ```text
  NUGET_PACKAGES=/workspace/.cache/nuget-packages \
  ```

  (no trailing slash).

- **Reproduction:**

  ```bash
  ./scripts/eshop.sh test e2e --agent
  ```

  Fails during the `dotnet restore (AppHost)` / `dotnet test (e2e)` step, while building
  `EShop.StoreFront.MigrationRunner.csproj` as an AppHost project dependency, with:

  ```text
  /workspace/src/EShop.StoreFront.MigrationRunner/EShop.StoreFront.MigrationRunner.csproj(98,5):
  error MSB3030: Could not copy the file
  "/workspace/.cache/nuget-packagesepiserver.cms.core/13.0.2/tools/EPiServer.Cms.Core.sql"
  because it was not found.
  Build failed with exit code: 1.
  ```

  Note the malformed path: `nuget-packagesepiserver.cms.core` - `nuget-packages` and
  `episerver.cms.core` are glued together with no `/` between them.

- **Does NOT reproduce:**
  - `./scripts/eshop.sh build --agent` (plain host build, not containerized) - succeeds.
  - `dotnet build src/EShop.StoreFront.MigrationRunner/EShop.StoreFront.MigrationRunner.csproj`
    run directly on the host - succeeds, real `EPiServer.Cms.Core.sql`/etc. get copied to
    `OptimizelySchemaScripts/` correctly (verified multiple times this session and in the
    prior session that built this target).
  - Only reproduces **inside the `eshop test e2e` container path** ("always
    containerized" per its own doc comment in `SellerPortalLoginTests.cs`).

- **Root cause (not yet fully confirmed - needs a follow-up session to verify inside
  the container directly):** `$(NuGetPackageRoot)episerver.cms.core\...` relies on
  `NuGetPackageRoot` already ending in a trailing slash - which is what a normal
  `dotnet restore`-generated `obj/<project>.csproj.nuget.g.props` always guarantees,
  regardless of how `NUGET_PACKAGES` itself is spelled. Inside the container,
  `NuGetPackageRoot` appears to evaluate to the env var's literal value
  (`/workspace/.cache/nuget-packages`, no trailing slash) instead of a
  slash-normalized value from the generated props file - possibly because the
  container's restore is skipped/cached in a way that never (re)generates that props
  file for this exact project in this exact container run, or because something in the
  container's MSBuild/SDK resolution order picks up the raw env var ahead of the
  generated property.

- **Suggested fix (pick one, verify empirically before considering this closed - do not
  assume):**
  1. Make the target robust to either spelling, independent of the root cause:

     ```xml
     <_NuGetPackageRoot>$([MSBuild]::EnsureTrailingSlash('$(NuGetPackageRoot)'))</_NuGetPackageRoot>
     ```

     then use `$(_NuGetPackageRoot)episerver.cms.core\...` instead of
     `$(NuGetPackageRoot)episerver.cms.core\...`. Cheapest, most robust - fixes the
     symptom regardless of why the property is missing its slash, and is harmless if
     `NuGetPackageRoot` already has the trailing slash (in the normal host build path).
  2. Actually root-cause it: shell into the utility container (`docker run` the same
     image `eshop test e2e` uses, or add a temporary diagnostic
     `<Target Name="PrintNuGetPackageRoot" BeforeTargets="CopyOptimizelySchemaScripts">
     <Message Text="NuGetPackageRoot='$(NuGetPackageRoot)'" Importance="high" /></Target>`)
     and confirm the actual value of `$(NuGetPackageRoot)` and whether
     `obj/EShop.StoreFront.MigrationRunner.csproj.nuget.g.props` exists/was regenerated
     for that container's restore. If the container's restore genuinely never writes
     that props file (e.g. a restore-cache-reuse path), fixing the restore step
     itself might be the more correct fix, not just tolerating the missing slash.
  3. Either way, add a regression test/check: this MSBuild target has no existing test
     coverage for the containerized path specifically (only `RealOptimizelyScriptsTests.cs`
     in `test/EShop.StoreFront.MigrationRunner.Tests`, which passes today because it runs
     outside the container).

### Cancelling an `EShop.Cli` command leaves the child process running

- **Area:** workflow
- **Location:** `src/EShop.Cli/Execution/ProcessRunner.cs:78`
- **Summary:** `ProcessRunner` passes the caller's cancellation token directly to
  `Process.WaitForExitAsync`, but it never terminates the started process when the token
  is cancelled. `Process.Dispose()` releases the .NET wrapper; it does not kill the OS
  process or its children.
- **Failure scenario:** A developer or agent cancels `eshop build`, `eshop test e2e`,
  or the container fallback for `eshop run` while `dotnet`, `pnpm`, `docker run`, or
  `aspire` is still active. The CLI task exits through cancellation, but the child keeps
  running in the background. For `docker run`/Aspire paths this can leave containers,
  test processes, or file writers alive after the command that owns them is gone.
- **Suggested fix:** Register the cancellation token after `process.Start()` and call
  `process.Kill(entireProcessTree: true)` when cancellation fires, then await exit and
  return or rethrow in one consistent way. Add a `ProcessRunner` test that starts a long
  sleep, cancels, and asserts the child process is gone.

## Medium

No finding in this review met the Medium bar.

## Low

### Container fallback for `eshop run` still treats EOF as "stop"

- **Area:** workflow
- **Location:** `src/EShop.Cli/AppHost/AppHostContainerScript.cs:53-58`,
  `src/EShop.Cli/Commands/RunCommand.cs:53-55`
- **Summary:** The native `AppHostSessionRunner` now treats stdin EOF as "keep running
  until cancellation", but the generated container fallback script still uses
  `while read -rsn1 X_KEY; do ... done; hj_stop`. If stdin is already at EOF, the loop
  exits immediately and stops the AppHost.
- **Failure scenario:** On a machine without a local Aspire CLI, an agent invokes
  `eshop run --agent < /dev/null` or from a harness that closes stdin. `RunCommand`
  enters the container fallback, `read` returns EOF immediately, and the script calls
  `aspire stop` right after `aspire start`, exiting as if the user pressed `q`.
- **Suggested fix:** Mirror the native path in the generated script: distinguish EOF
  from `q`, and block until `INT`/`TERM` when stdin is closed. Add an
  `AppHostContainerScriptTests` assertion for the EOF branch.

### E2E test setup is copy-pasted across four test classes

- **Area:** maintainability
- **Location:** `test/EShop.AppHost.E2ETests/SellerPortalLoginTests.cs:26-63`,
  `test/EShop.AppHost.E2ETests/SellerPortalMovieDraftSubmissionTests.cs:36-82`,
  `test/EShop.AppHost.E2ETests/SellerPortalMerchandiseDraftSubmissionTests.cs:36-76`,
  `test/EShop.AppHost.E2ETests/SellerDraftApprovalSimulatorLivePushTests.cs:28-70`
- **Summary:** The AppHost bootstrap, health waits, HttpClient setup, Playwright launch,
  timeout setup, and login helper logic are duplicated across every browser E2E test
  class. The duplication has already spread as the suite grew.
- **Failure scenario:** A future change to the login flow, resource health waits,
  browser context setup, or timeout policy must be applied in several places. Missing
  one copy produces a slow failure after a full Aspire stack startup, which makes the
  suite harder to maintain safely.
- **Suggested fix:** Extract a shared E2E fixture/helper for AppHost startup and browser
  login while preserving the existing one-test-method-per-stack strategy that keeps
  runtime predictable.

### Several code comments still point at deleted scripts and deleted OS bootstrap paths

- **Area:** documentation
- **Location:** `src/EShop.Cli/Execution/IContainerRunner.cs:19-20`,
  `src/EShop.Cli/Execution/IUtilityImageProvisioner.cs:19`,
  `src/EShop.Cli/Execution/ExecutionMode.cs:19-21`,
  `src/EShop.Cli/Program.cs:30-31`,
  `src/EShop.Cli/Repo/RepoRootLocator.cs:19-20`
- **Summary:** Some current code comments still reference deleted `scripts/internal/*.bash`
  helpers or the removed `.zsh`/`.ps1` bootstrap paths. One comment even says "ADR
  0016d", which is not an ADR.
- **Failure scenario:** A new contributor or agent follows a comment to understand the
  current CLI flow, searches for the named script or ADR, and finds nothing. That is
  exactly the drift the comment-consolidation work tried to remove.
- **Suggested fix:** Remove historical "replaces script X" comments from code, or point
  to `doc/CHRONICLE.md`/ADR 0016 where the historical mapping now lives. Update the
  `.zsh`/`.ps1` comments to the current Linux-only bootstrap path.

## Resolved since last review

- `eshop test e2e` cannot reach the Docker daemon on rootless Docker — resolved by
  resolving `DOCKER_HOST`/`XDG_RUNTIME_DIR` in `DockerSocketPathResolver` and exporting
  the resolved `DOCKER_HOST` into the utility container.
- `eshop screenshot` captures before client-side rendering finishes — resolved by the
  Playwright-based screenshot runner with login support and a network-idle/hard-timeout
  settle step.
- Stale `.trx` files poison test reports — resolved by `TestResultsDirectory.ClearStale`
  in the unit, coverage, and E2E test commands.
- `eshop build` drops analyzer/format diagnostics in container mode — resolved by using
  a repo-relative format report path and by including the format result in the build
  failure calculation.
- `X-Forwarded-Host` is trusted from any client — resolved by keeping ASP.NET Core's
  default trusted proxies/networks and by deriving `X-Forwarded-Host` from the incoming
  `Host` header in the Next.js proxy.
- Draft image routes can leave dangling blob references after DB failures — resolved
  for the user-visible dangling-reference cases by committing DB row removals before
  publishing blob-deletion work.
- Draft update endpoints have no request validation — resolved by `DraftValidation` and
  the ownership check in `ReconcileFormatVariants`.
- The arm64 AppHost/E2E limitation was undocumented — resolved by documenting the
  x86-64 requirement in `README.md`, `AGENTS.md`, and `doc/TODO.md`, and by adding the
  prerequisite warning.
- `Directory.Build.props` writes `.sarif` files into project directories — resolved by
  writing under `obj/`.
- `eshop restore` writes `restore.binlog` to the repository root — resolved by writing
  it under `tmp/` and deleting it after a successful restore.
- `--tools auto` uses mismatched local tool versions — resolved by marking mismatched
  local tools unusable so `auto` falls back to the container.
- The deep-review prompt generated an `ISSUES.md` header that failed markdown lint —
  resolved by using the plain `Last deep review: <date>` line.
- The AppHost already-running guard missed container-mode Aspire sessions — resolved by
  comparing a normalized repo-relative path suffix.
- `SubmissionNotificationBroadcaster` could orphan a subscriber — resolved by retrying
  if the list returned by `GetOrAdd` was removed before the subscriber lock was taken.
- The submission SSE loop accumulated abandoned channel waiters — resolved by keeping
  one outstanding `WaitToReadAsync` task per connection. The inventory SSE endpoint uses
  the same fixed pattern.
- The utility container image was not rebuilt after source changes — resolved by hashing
  the Containerfile and version-source files and storing the hash as a Docker label.
- The Seller inventory slice was half-built and undocumented — resolved by adding the
  report-inventory endpoint/UI, DevTools simulator, tests, and documentation.
- The DevTools SKU generator truncated only timestamp bits — resolved by taking random
  trailing UUIDv7 characters.
- EF migrations were not checked against the model — resolved by adding
  `dotnet ef migrations has-pending-model-changes` to `eshop build`.
- `SaveTokens = true` contradicted the documented invariant — resolved by documenting
  the deliberate exception and by correcting `OidcTokenPruning` to keep `id_token` for
  OIDC logout while still stripping `access_token`.
- Native `eshop run` treated stdin EOF as a stop request — resolved for the native path
  by keeping the AppHost running until cancellation after EOF. The container fallback
  remains open above.
- `shellcheck` tried to cover deleted `.bash`/`.zsh` paths — resolved by checking only
  the Linux `.sh` bootstrap path.
- `@types/node` lagged behind `.nvmrc` with no explanation — invalidated as a defect by
  the `doc/CHRONICLE.md` entry that records the lag as a deliberate, accepted tradeoff.
- DevTools completes durable submission messages into an in-memory store — invalidated
  as a defect. DevTools is a developer tool and deliberately does not require
  persistence; a Seller can recover a stranded `PendingReview` draft through the
  `Cancel review` button on the draft edit page.
- `PLAN-1.md` was stale and contradicted current code — resolved by deleting the stale
  root-level plan file.
- Keycloak provisioning errors surfaced `KeyNotFoundException` — resolved by using
  `GetRequiredField`/`TryGetValue`.
- `eshop run` reported success after AppHost start failures — resolved by propagating
  native and container fallback exit codes.
- Failed schema regeneration reported "0 errors" with no diagnostic — resolved by
  emitting the `openapi-typescript` stdout/stderr as a build diagnostic.
- `PublishSubmissionRequestAsync` caught only `ServiceBusException` — resolved by
  broadening the handled publish-failure path.
- ADR 0011 did not point at ADR 0016's replacement commands — resolved by adding the
  Status note.
- `doc/MEMORY.md` missed current E2E coverage and inventory state — resolved by updating
  the repository-state section.
