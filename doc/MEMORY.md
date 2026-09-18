# Memory

## Current State

A demo eCommerce website built on .NET 10, Optimizely CMS 13, and Optimizely Commerce
Connect 15, orchestrated with Aspire 13. The store is a movie store: it sells movies
(DVD, Blu-ray, streaming entitlement) and movie merchandise, supplied by third-party
Sellers in a marketplace model. See `README.md` for the project pitch and `doc/SPEC.md`
for the full domain rules.

### Repository layout

- `src/` has the Aspire AppHost scaffold (`EShop.AppHost`), `EShop.ServiceDefaults`
  (OpenTelemetry/service discovery/resilience/health-check wiring, usable from both the
  minimal-hosting model and the classic `Startup.cs` model), `EShop.Common`
  (cross-project constants, environment-detection helpers, referenced by every project),
  the Seller Portal (`EShop.SellerPortal.Bff`/`.Web`), its message contracts
  (`EShop.Messaging`), development-only workflow tooling (`EShop.DevTools`), the
  developer CLI (`EShop.Cli`), and the Storefront (`EShop.StoreFront.Web`, scaffolded and
  wired into `AppHost.cs`). No CMS/Commerce catalog content types, no Profile
  microservice, and no search microservice exist yet.
- The migration model (ADR 0023) spans three layers: `EShop.Migrations.Common` (shared
  constants in `MigrationNames`, plus `SqlCommandExtensions`/`SqlNullableValueExtensions`
  raw ADO.NET plumbing), `EShop.Migrations.Orchestration` (technology-agnostic runner
  primitives: `SchemaMigrationLock`, `SchemaMarkerStore`, `SqlExceptionRetry`,
  `MigrationReadinessWaiter`), and two technology-specific runners built on top:
  `EShop.SellerPortal.MigrationRunner` (EF Core, `Database.MigrateAsync()`) and
  `EShop.StoreFront.MigrationRunner` (Optimizely's own shipped SQL scripts, via
  `EShop.Migrations.Optimizely`). Each runner is the only project for its component - no
  separate constants project per component. `EShop.Common`'s `KnownNames` holds no
  migration-specific naming - general-purpose Aspire/identity naming only.

### AppHost (`src/EShop.AppHost/AppHost.cs`)

- Hosts: SQL Server for the Seller Portal (ADR 0003; Azure SQL Database once deployed,
  ADR 0019), Azure Service Bus for Seller Portal ↔ Commerce Connect messaging (ADR 0009),
  Azure Blob Storage for submission-image claim checks (ADR 0009), Redis for the Seller
  Portal's auth ticket store and Data Protection key ring (Azure Cache for Redis once
  deployed, ADR 0021, superseding ADR 0012's Valkey), and Keycloak for local development
  only (ADR 0010; Microsoft Entra External ID once deployed, ADR 0020). The Storefront
  search resource will sit behind a search microservice (Elasticsearch locally, Azure AI
  Search deployed, ADR 0022/0024) once that slice is scaffolded.
- Every resource, including Keycloak, has `WithLifetime(ContainerLifetime.Persistent)`
  (ADR 0025) - but Keycloak alone has no `WithDataVolume()`: its `--import-realm` only
  imports a realm that doesn't already exist, so persisting its database would stop
  edits to `src/EShop.AppHost/Realms/eshop-realm.json` from ever taking effect again.
  That realm file is the sole source of both local realm data and the Seller
  Portal/Storefront OIDC client configuration - there is no runtime/dynamic client
  provisioning.
- The AppHost SDK and its integration packages are pinned to `13.5.3`; the preview-only
  Keycloak integration is `13.5.3-preview.1.26425.3`. `AspireUseCliBundle` is `true`
  (AppHost orchestration uses the compatible Aspire CLI bundle, not NuGet-restored
  orchestration dependencies). An Azure Container Apps environment resource
  (`AddAzureContainerAppEnvironment`, ADR 0018) is added only when publishing.

### Dev reverse proxy (`src/dev/EShop.ReverseProxy`)

- Dev-only AppHost resource (`KnownNames.ResourceDevReverseProxy` = `"reverse-proxy"`)
  wrapping the `HenrikJensen.ReverseProxy`/`HenrikJensen.ReverseProxy.Aspire` packages
  from the `lib/DotNet-ReverseProxy` submodule (currently a direct `ProjectReference`,
  not a centrally-pinned NuGet package - blocked on that package's NuGet publication,
  gated on `PLAN-1.md`). It gives one fixed HTTPS endpoint on
  `KnownNames.ReverseProxyHttpsPort` (`8443`) and routes `identity`/`seller`/
  `storefront.eshop.local` (`KnownNames.ReverseProxyIdentityHostName`/
  `ReverseProxySellerPortalHostName`/`ReverseProxyStorefrontHostName`) to Keycloak,
  Seller Portal Web, and Storefront. Seller Portal Web and Storefront no longer call
  `WithExternalHttpEndpoints()` locally (only in the publish-mode branch).
- `PLAN-2.md` (replace dynamic `localhost` browser endpoints with one stable local HTTPS
  ingress) is done in full (§1-§7): AppHost model tests cover the reverse-proxy resource
  itself; E2E tests cover Keycloak discovery/issuer, cross-app SSO isolation
  (`CrossAppSsoAuthorizationTests`), and forwarded-header spoofing protection
  (`ForwardedOriginTrustTests`); [ADR 0025](./adr/0025-local-development-reverse-proxy.md)
  documents the decision. `README.md`/`DEVELOP.md`, `AGENTS.md`'s screenshot examples,
  `doc/System landscape.md`, and the C4 container diagram already reflect this. See
  `doc/CHRONICLE.md` for the build-out history. A known unresolved routing risk (the
  reverse proxy's own `/health` can shadow a target's `/health` on every public
  hostname) is tracked in `doc/TODO.md`, not here.
- `src/dev/EShop.ReverseProxy/appsettings.json`'s `SelfSignedCertificate:CaFilePath` is a
  `{REVERSEPROXY_HOME}` placeholder the package substitutes from the `REVERSEPROXY_HOME`
  env var. `AppHost.cs` sets it (unless already set) to `~/.reverseproxy` and creates the
  directory eagerly, failing fast if it can't be created/written to. Native execution and
  the containerized `eshop run` fallback deliberately end up with two independent CAs -
  native `~/.reverseproxy`; containerized `<repo-root>/.cache/home/.reverseproxy`, since
  the Containerfile sandboxes `$HOME` - this is intentional policy, not a gap to close.
  Do not invent a constant for the `REVERSEPROXY_HOME` name itself - it is a fixed
  external contract the package reads verbatim (eShop code only centralizes its own
  spelling of the name, as `KnownNames.ReverseProxyHomeEnvVarName`).

### Testing

- `test/EShop.AppHost.Tests` (xUnit 3, `Aspire.Hosting.Testing`) asserts AppHost resource
  registration with no containers started. `test/EShop.AppHost.E2ETests` starts the real
  AppHost and drives headless Chromium via `Microsoft.Playwright` (ADR 0011): Seller
  Portal login, the Movie/Merchandise draft submission loops, the Seller Draft Approval
  Simulator's SignalR live push, Storefront OIDC/`/ui/cms`, Keycloak discovery, cross-app
  SSO isolation, and forwarded-origin trust. `test/EShop.StoreFront.Web.Tests` covers the
  Storefront's `Foundation.Operations.OperationExtensions` helpers as fast unit tests.
- The Docker-backed E2E tests are deliberately not part of `EShop.slnx` (slower, need
  Docker) - they only run through `eshop test e2e`. `eshop test e2e --filter-class
  <fully-qualified-class>` (or `--filter-method`, mutually exclusive with
  `--filter-class`) runs one focused test; VSTest's `--filter` is unsupported. Normal
  Seller Portal E2E test starts and disposes its own full AppHost and browser;
  `[assembly: CollectionBehavior(DisableTestParallelization = true)]` runs them one at a
  time so they don't starve each other's containers. A shared-AppHost fixture was tried
  and reverted after it caused cross-test interference - see `doc/CHRONICLE.md`. Every draft-creation E2E flow waits
  for the initial empty-drafts state before clicking (avoids a hydration race), then
  asserts the creation POST response within 30 seconds. `E2ETestHarness` records only
  request method/path/status and sanitizes console diagnostics on timeout.

### Diagrams and CLI

- `doc/c4/` has a System Context diagram, a Container diagram, and one Component diagram
  (Seller submission/review, ADR 0008/0009), each a `.puml` source rendered to `.svg` via
  `eshop diagram render`. Other component diagrams are deferred - see `doc/TODO.md`.
- `EShop.Cli` (ADR 0016; Linux only for now, ADR 0017) is the one entry point for
  restore/build/format/test (unit, coverage, e2e)/run/diagram render/screenshot/
  generate-types, started via `scripts/eshop.sh`. `IToolExecutor` runs each tool locally
  or inside the `eshop-utility` Containerfile image, per `--tools auto` (local-first,
  default), `local`, or `container`. `eshop build`/`restore`/`test` (unit) run their
  independent steps concurrently (`Task.WhenAll`); `test e2e`/`test coverage` stay
  sequential. `HumanOutputSink` shows a live spinner; `AiOutputSink` (`--agent`) writes a
  once-a-minute liveness heartbeat and scopes `NO_COLOR` to itself only.

### Seller Portal

- Implements both Movie and Merchandise draft flows from `doc/SPEC.md` end-to-end:
  create, edit, delete, submit for review. The BFF issues and enforces its own
  antiforgery token on every mutating route. A submission's outcome (`Approved` with an
  assigned SKU, or `Rejected` with a reason) stays visible on its own history view even
  after the originating Draft is gone. `src/apps/EShop.SellerPortal.Web` uses Tailwind
  CSS v4 and shadcn/ui components.
- The Seller Portal/Commerce Connect message contract (`src/shared/EShop.Messaging`,
  explicitly PROVISIONAL per ADR 0009) is kind-agnostic: one `SubmissionRequestMessage`
  carries either a Movie or a Merchandise submission's fields, with image blob
  references. The Service Bus consumer base class and message types live there (not the
  Bff), so `EShop.DevTools` can reuse them without a Service Bus SDK dependency in
  `EShop.Common`. That base class runs in-process inside the hosting app, not a separate
  worker - an accepted trade-off for this demo's message volume.
- `HybridCacheTicketStore`/`OidcSignOutTokenRefresh` live in the shared
  `src/shared/EShop.TicketStore` project, used by both `EShop.SellerPortal.Bff` and
  `EShop.StoreFront.Web` (each registers its own instance with its own Redis key
  prefix).
- The Seller inventory slice (ADR 0007) is wired end-to-end: a Seller reports a quantity
  for one of their own approved SKUs from `/inventory`; the Bff validates ownership,
  upserts a `SellerInventory` row, and publishes an inventory-report message;
  `EShop.DevTools`' Inventory Report Simulator publishes back Confirmed/Failed; a
  Bff-side consumer updates the row's sync status accordingly.
- The Seller Portal shows toast notifications: immediate on save/submit, plus a pushed
  one when a submission is approved/rejected while the Seller is anywhere in the portal.
  The push side is Server-Sent Events: a single-process, in-memory
  `SubmissionNotificationBroadcaster` fans a submission's outcome out to that Seller's
  open connection(s); a Seller with none simply misses the push (`GET
  /bff/api/submissions` stays independently authoritative).
- The BFF's ASP.NET Core Data Protection key ring persists into Redis
  (`RedisXmlRepository`), so `seller-portal`/`XSRF-TOKEN` cookies survive an `aspire
  start` restart. `AntiforgeryEndpointFilter`/`lib/bff-fetch.ts` also self-heal a stale
  antiforgery pairing (refresh and retry once).
- The Bff's `Contracts/*.cs` are the source of truth for the data contracts
  `lib/types.ts` used to define by hand (ADR 0014). The Bff generates an OpenAPI
  document at build time; `eshop generate types` turns it into a committed
  `lib/api-schema.d.ts` via `openapi-typescript`, and `lib/types.ts` only re-exports
  from it. `eshop build` fails when the committed file goes stale; `eshop format`
  regenerates it. Contracts' status/kind/format properties carry the real C# enum (a
  global `JsonStringEnumConverter` keeps the JSON wire format unchanged).
- Per ADR 0018, `AppHost.cs` no longer chains `.WaitFor(...)` onto the Bff's (or
  DevTools') dependencies - each dependency's resilience is the consuming service's own
  concern: the Bff's `/health` aggregates each Aspire client integration's own
  auto-registered health check (SQL, Service Bus, Blob Storage, Redis);
  `ServiceBusQueueConsumer` wraps its initial `StartProcessingAsync` in a Polly retry
  pipeline; the Redis cache resource needs no such treatment (`AbortOnConnectFail =
  false` by default, and both local and deployed now use the same
  `AddAzureManagedRedis(...).RunAsContainer()` hybrid resource). The Seller Portal
  database follows ADR 0023's migration model: `EShop.SellerPortal.MigrationRunner`
  applies pending EF Core migrations, holding a `sp_getapplock`-based exclusive lock
  (`SchemaMigrationLock`, keyed on `KnownNames.MigrationsSellerPortalLockName`) and
  writing a `SchemaMigrationMarkers` row (`SchemaMarkerStore`); the Bff no longer
  migrates its own database at startup, and its `SchemaMarkerHealthCheck` (part of
  `/health`, not `/alive`) reports not-ready until that marker shows success. Both the
  runner's connection-open and its migration call are wrapped in `SqlExceptionRetry`. See
  `doc/CHRONICLE.md` for why this replaced `Aspire.Hosting.EntityFrameworkCore`'s
  `AddEFMigrations`.

### Commerce Connect / DevTools

- Commerce Connect (the Merchandiser-review counterpart to the Seller Portal's
  submission flow) has not been scaffolded yet. `src/dev/EShop.DevTools` is a dev-only
  Razor Pages site (gated out of a real deployment by Aspire's publish-mode check) where
  a developer can inspect and drive workflow states; some flows are not meant for
  Merchandisers, and some cannot be expressed through CMS approval sequences. It remains
  developer-only tooling even once the Storefront exists - the Storefront replaces it
  only where real Commerce Connect behavior is required. Its "Seller Draft Approval
  Simulator" page updates live via `SellerSubmissionsHub` (SignalR, ADR 0015).

### Storefront

- `EShop.StoreFront.MigrationRunner` extends ADR 0023 to Optimizely CMS/Commerce: one
  binary (selected by a `cms`/`commerce` argument) applies Optimizely's own shipped SQL
  scripts (`EShop.Migrations.Optimizely`'s `OptimizelySqlScriptRunner`/
  `OptimizelyPackageScriptLocator`) rather than an EF Core migration, reusing
  `EShop.Migrations.Orchestration`'s lock/marker/retry unchanged.
  `CopyOptimizelySchemaScripts` (an MSBuild target in the runner's own `.csproj`) copies
  the real `episerver.cms.core`/`episerver.commerce.core` `tools/` scripts into its
  build output. Wired into `AppHost.cs` as two runner resource instances
  (`.WithArgs("cms")`/`.WithArgs("commerce")`), with no `.WaitFor`. See
  `doc/CHRONICLE.md` for the verification history.
- `nuget.config` has a second source, Optimizely's own feed
  (`https://nuget.optimizely.com/feed/packages.svc/`), scoped via package source mapping
  to `EPiServer`/`EPiServer.*`/`Optimizely.*` only - every other package still resolves
  from `nuget.org`.
- `EShop.StoreFront.Web` is scaffolded and wired into `AppHost.cs` (from
  `epi-commerce-empty`, on .NET 10/CMS `13.1.3`/Commerce Connect `15.2.0`). It uses the
  classic `Startup.cs` hosting model, not this repo's usual minimal-hosting pattern
  (`AddCms()`/`AddCommerce()` do not work under `WebApplication.CreateBuilder` - see
  `doc/CHRONICLE.md`); `EShop.ServiceDefaults` supports both models from one
  implementation. Two extra required package references, beyond the `EPiServer.CMS`/
  `EPiServer.Commerce` meta-packages, remain necessary. `AddCmsAspNetIdentity` (the
  template's own default) is dropped, not adapted - ADR 0002 already makes external OIDC
  the identity system of record. `DataAccessOptions.UpdateDatabaseSchema`/
  `CreateDatabaseSchema` are both explicitly `false`, plus a blocking `ISchemaValidator`
  (ADR 0023 - normal startup must never mutate schema). Readiness is
  `StorefrontMigrationPreflight.cs`, not `.WaitFor` or a health check (Optimizely's own
  `DatabaseSchemaHost` crashes the whole app at startup on a missing schema, and
  `.WaitFor` has no effect once deployed to Azure Container Apps): `Program.cs` checks
  `SchemaMarkerStore` for both components itself, before ever building the real host.
  `EPiServer.Data`'s connection-string convention needs exactly `EPiServerDB`/
  `EcfSqlConnection` (not this repo's usual kebab-case Aspire resource names), bridged in
  `AppHost.cs` via `.WithEnvironment("ConnectionStrings__EPiServerDB", ...)` rather than
  `.WithReference`. Still has no content types - that's a later phase.
- Storefront authentication foundation is done.
  `EShop.StoreFront.Web/Initialization/AuthConfiguration.cs` configures Keycloak for
  local development and Microsoft Entra External ID for a deployed instance. Storefront
  has no custom `/login`, `/logout`, or `/user` endpoints; its protected `/ui/cms` route
  starts the OIDC challenge. It calls neither `AddCmsAspNetIdentity()` nor
  `EPiServer.OptimizelyIdentity`'s `AddOptimizelyIdentity()`, and instead calls the
  public `EPiServer.Security.ISynchronizingUserService.SynchronizeAsync` after a
  successful external sign-in, requiring non-empty `preferred_username`, `email`,
  `given_name`, and `family_name` claims first - it rejects an incomplete ticket and
  never manufactures profile data. `ClaimTypeOptions` maps these custom claim names
  during service registration, before DI is built - not from the deferred OpenID Connect
  options callback; see `doc/CHRONICLE.md` for why. External roles map onto Optimizely's
  role vocabulary via the public `AddMappedRole` virtual-role API
  (`ContentEditor`→`CmsEditors`, `SiteAdministrator`→`CmsAdmins`,
  `Merchandiser`→`CatalogManagers`, plus a reserved, not-yet-consumed custom
  `Merchandisers` role). The Storefront's Keycloak OIDC client, like the Seller Portal's,
  comes solely from the static realm import. Later phases (a custom Optimizely Shell
  tool for Merchandiser submission review, then the Commerce/CMS content types and
  Storefront shell pages) are identified but not started - see `doc/TODO.md`.

### Upstream work

- `PLAN-1.md` is approved: it adds `lib/DotNet-ReverseProxy` as an independently owned
  Git submodule, unrelated to eShop except for the Git reference - read and follow its
  own `AGENTS.md`/`.editorconfig` when changing or testing it. The local upstream branch
  `feature/aspire-13-5-gateway-support` (based on upstream `develop`) implements the
  approved opt-in forwarded-origin mode and upgrades that project's own Aspire hosting
  and example AppHost to `13.5.3`. See `doc/CHRONICLE.md` for detail.
- `aspire init` has been run; agent skills and MCP configuration were added locally and
  are gitignored (see `DEVELOP.md`'s "Agent Coding Harness" section).

## Conventions and Constraints

- To add a new dev tool to `EShop.DevTools`, create a folder under
  `Pages/Tools/<Name>/`, link it from `Pages/Index.cshtml`, and add a nav entry to
  `Pages/Shared/_Layout.cshtml` - there is no config-driven tool registry.
- Update this file whenever a session ends or reaches a good pause point - keep it
  current-state only, not a changelog. Historical reasoning, past incidents, discarded
  alternatives, and completed migrations belong in `doc/CHRONICLE.md` instead.
- Do not push any local commits from the `lib/DotNet-ReverseProxy` submodule's upstream
  work to a remote until `PLAN-1.md` is complete and the user has finished code review.

## Non-obvious Current Constraints

Surprising or important constraints future work must not accidentally violate.

- No `secret: true` `AppHost.cs` parameter (`keycloakAdminPassword`,
  `sellerPortalOidcClientSecret`, `storefrontOidcClientSecret`, and - publish-mode only,
  no Development default at all - `entraClientSecret`/`storefrontEntraClientId`/
  `storefrontEntraClientSecret`) may get a literal default in code; a Development-only
  value belongs in that project's own `appsettings.Development.json` only. Forgetting
  one throws `Aspire.Hosting.MissingParameterValueException` at AppHost startup, not at
  build time - easy to miss until something actually tries to run.
- `scripts/Containerfile` sets `NUGET_PACKAGES=/workspace/.cache/nuget-packages` with no
  trailing slash, unlike a native restore's own default (which always ends in one).
  `CopyOptimizelySchemaScripts.targets` must wrap `$(NuGetPackageRoot)` in
  `$([MSBuild]::EnsureTrailingSlash(...))` before appending a package folder name - the
  bare property concatenated a folder name directly onto the path with no separator,
  which built fine natively but broke only when restoring/building inside the
  container.
- `Aspire.Hosting.Keycloak` has never published a stable (non-preview) release, even
  though Aspire itself is stable - check before bumping its pinned version.
- `Directory.Packages.props`'s `SQLitePCLRaw.bundle_e_sqlite3` (pinned to 2.1.12) is
  deliberately newer than ADR 0013's 7-day quarantine would otherwise allow. It is a
  documented security exception because the "compliant," older version has a disclosed
  high-severity CVE. Do not align it with nearby pins at the next quarantine review.
- `eshop test e2e`/`eshop run` refuse to start while a native AppHost session for this
  project is already up (`IAppHostGuard`) - running two at once corrupts
  `node_modules` (both write to the same bind-mounted folder). The guard has a known
  gap: it can't see a running session on the *other* side (native vs. containerized).
- `src/apps/EShop.SellerPortal.Web/next.config.ts`'s `NEXT_DIST_DIR`-driven `distDir` must
  stay wired (native → default `.next`, containerized → `.next-container` via
  `scripts/container.env`) so a native and a containerized run never share a build
  cache with the other's wrong absolute paths baked in.
- `eshop run`'s `--no-build` requires `eshop build` to have already succeeded, and
  build and run must happen on the same side (both native, or both containerized) -
  the Aspire SDK bakes the AppHost's absolute project directory into a compile-time
  constant the Keycloak realm import resolves against. Switching sides needs a
  rebuild first.
- The Bff's own startup code skips every registration needing a real Aspire-provided
  connection (SQL Server, Service Bus, Blob Storage, Redis, the Keycloak/Entra OIDC
  handler) when it detects build-time OpenAPI generation, registering a few
  non-connecting stand-ins instead - required so Minimal API's parameter-binding
  inference has a registered service to find; skipping this crashes doc generation.
- `builder.ExecutionContext.IsPublishMode` (`AppHost.cs`) must stay there, not move to
  `EShop.Common` - its type only exists via `Aspire.Hosting.AppHost`, which pulls in
  native DCP orchestration binaries no other project needs.
- `EShop.Cli`'s `--tools local` never falls back to the container even if a tool is
  missing (only `auto`/`container` do); `eshop test e2e` and `eshop screenshot` always
  force the container regardless of `--tools`, since only it has Chromium.
- `eshop screenshot --login` requires a same-origin `--login-path`, `--username`, and
  `--password`; it has no resource-specific defaults. The caller supplies the path that
  starts that resource's OIDC login: `/bff/login` for the Seller Portal and `/ui/cms`
  for the Storefront. The screenshot storage-state path derives from the target origin,
  login path, and username, so cookies for different `localhost` ports and users do not
  mix. Repeat the same login options to reuse that state; delete
  `tmp/screenshot-profile` to remove every saved screenshot state.
- `EShop.DevTools`' SignalR hubs (`SellerSubmissionsHub`, `SellerInventoryHub`) and Razor
  Pages have no authentication - acceptable only because the project is dev-only tooling
  excluded from a real deployment.
- `ClaimTypeOptions` (Storefront) must map its custom claim names during service
  registration, before DI is built, not from the deferred OpenID Connect options
  callback - see `doc/CHRONICLE.md` for the defect that caused.

## Open Follow-ups

- Storefront's later phases (a custom Optimizely Shell tool for Merchandiser submission
  review, then Commerce/CMS content types and Storefront shell pages) and scaffolding
  the search and Profile microservices are tracked in `doc/TODO.md`.
- The two Storefront migration runners' `ToolsDirectory`/script-folder-name
  configuration is still a literal, separate from `Directory.Packages.props`'s pin -
  only the version number is derived automatically (`OptimizelyInstalledVersion`,
  reading the actual referenced assembly). See `doc/CHRONICLE.md`.
- `PLAN-1.md`'s upstream pull request/merge/NuGet publication, its later automated
  forwarding tests, and its documentation/release-candidate work remain open (tracked in
  `PLAN-1.md` itself).
- See `doc/TODO.md` for the rest of the open and deferred questions (including Azure
  Container Apps migration-job gating and the reverse proxy's `/health` routing risk),
  and `ISSUES.md` for open infrastructure defects.
