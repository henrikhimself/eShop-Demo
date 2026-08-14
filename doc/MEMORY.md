# eShop Session Memory

This document is the eShop demo project's *current* state only: current architecture,
conventions, and constraints an agent needs before working on the code. It is a process
document, like `TODO.md` - not written in ASD-STE100, not part of the specification.
Historical reasoning, past incidents, discarded alternatives, and completed migrations
belong in `CHRONICLE.md` instead; this file points to them rather than repeating them.
Read `AGENTS.md` first in any new session - it explains the role of `SPEC.md`,
`System landscape.md`, `doc/adr/`, `doc/c4/`, `TODO.md`, and `CHRONICLE.md`, and the
writing rules for each. Update this file whenever a session ends or reaches a good
pause point - keep it current-state only, not a changelog.

## Current State

A demo eCommerce website built on .NET 10, Optimizely CMS 13, and Optimizely Commerce
Connect 15, orchestrated with Aspire 13. The store is a movie store: it sells movies
(DVD, Blu-ray, streaming entitlement) and movie merchandise, supplied by third-party
Sellers in a marketplace model. See `README.md` for the project pitch and `doc/SPEC.md`
for the full domain rules.

### ADR index

1. Single combined Optimizely CMS + Commerce Connect site (via `epi-commerce-empty`,
   with separate CMS and Commerce databases).
2. External OpenID Connect IdP is system of record for all identities.
3. Seller Portal is a separate application, isolated from Commerce Manager. (Amended by
   ADR 0007's Status note re: inventory visibility.)
4. Profile microservice fills Shopper-data gaps in Commerce Connect Customer Management.
5. Movie catalog modeled as a product with variants (DVD/Blu-ray/streaming); Storefront
   merchandise now also needs product and variant types for purchasable options such as
   T-shirt sizes.
6. Marketplace: multiple Sellers can compete on one SKU; automatic deduplication with
   Merchandiser review.
7. Seller-reported inventory through a per-seller warehouse (amends ADR 0003).
8. Submission modeled as a hidden, lightweight content type, reviewed via native
   Optimizely Content Approval (not the live catalog product/variant).
9. Durable messaging (Azure Service Bus via Aspire, with Azure Blob Storage claim-check
   for images) between the Seller Portal and Commerce Connect, for both integration
   legs.
10. Keycloak is the identity provider product (amends ADR 0002). Aspire hosts a Keycloak
    container for local development.
11. Playwright drives the browser end-to-end tests.
12. Valkey backs the Seller Portal's auth ticket store and Data Protection key ring
    (amends ADR 0003's Status note re: session persistence). Superseded by ADR 0021.
13. Every dependency is pinned to one exact version, updated only after a 40-day
    quarantine.
14. The Bff's OpenAPI document is the source of truth for the Seller Portal Web's
    generated types.
15. SignalR gives the DevTools Seller Draft Approval Simulator page a live-update push,
    with a full page reload as the update mechanism.
16. `EShop.Cli`, a Spectre.Console.Cli-based .NET tool, replaces the developer-facing
    role of the old `scripts/*.bash` collection.
17. `EShop.Cli` supports Linux only, for now (narrows ADR 0016's "three small per-OS
    scripts" statement); Windows/macOS support is a deferred future task.
18. Azure Container Apps is the deployment target. Aspire's own `WaitFor` startup
    ordering does not carry over to a deployed instance, so the Bff's dependencies each
    get their own resilience instead of an AppHost-level wait (see Repository state).
19. Azure SQL Database is the managed SQL service for a deployed instance; local
    development keeps its SQL Server container (one hybrid AppHost resource covers
    both, no code branching).
20. Microsoft Entra External ID is the identity provider for a deployed instance
    (amends ADR 0010 - Keycloak stays local-development-only). A Site Administrator
    onboards a new Seller via the Entra admin center's "Invite external user" flow,
    with the Seller App Role assigned before the invite is sent.
21. Redis, not Valkey, backs the Seller Portal's auth ticket store and Data Protection
    key ring, for local development and a deployed instance alike (supersedes ADR
    0012 - Redis 8.0's AGPLv3 licensing option resolves the concern that picked Valkey).
22. Storefront search uses Elasticsearch for local development and Azure AI Search for a
    deployed instance. The indexed search document is the stable contract across both
    backends.
23. Database schema changes run through explicit migration resources, not normal
    application startup. The shared safety model is migration locks, schema markers, and
    readiness checks; local development must not rely on Aspire-only completion ordering.
24. Storefront search goes through a search microservice. Its first slice exposes a
    basic API and health checks only; indexing/query endpoints wait for the search
    document shape.

### Repository state

- `src/` has the Aspire AppHost scaffold (`EShop.AppHost`), `EShop.ServiceDefaults`
  (OpenTelemetry/service discovery/resilience/health-check wiring - now usable from both
  the minimal-hosting model and the classic `Startup.cs` model, see the
  `EShop.StoreFront.Web` bullet below), `EShop.Common` (cross-project constants,
  environment-detection helpers, referenced by every project), the Seller Portal
  (`EShop.SellerPortal.Bff`/`.Web`), its message contracts (`EShop.Messaging`),
  development-only workflow tooling (`EShop.DevTools`), the developer CLI (`EShop.Cli`),
  and a first scaffold of the Storefront (`EShop.StoreFront.Web`, not yet wired into
  `AppHost.cs` - see its own bullet below). No CMS content types, no Commerce catalog
  content types, no Profile microservice, and no search microservice exist yet. The
  migration model (ADR 0023) now spans three layers: `EShop.Migrations.Common` holds
  what both other layers need (`MigrationNames`' component/lock-name constants; raw
  ADO.NET plumbing shared by both - `SqlCommandExtensions`/`SqlNullableValueExtensions`),
  `EShop.Migrations.Orchestration` holds the technology-agnostic runner primitives built
  on top of it (`SchemaMigrationLock`, `SchemaMarkerStore`, `SqlExceptionRetry`,
  `MigrationReadinessWaiter`), and `EShop.SellerPortal.MigrationRunner` (EF Core,
  `Database.MigrateAsync()`) and `EShop.StoreFront.MigrationRunner` (Optimizely's own
  shipped SQL scripts, via `EShop.Migrations.Optimizely`, which also depends on
  `EShop.Migrations.Common`) are the two technology-specific runners built on top of
  that - each runner is the only project for its component (no separate constants
  project per component). `EShop.Common`'s `KnownNames` holds no migration-specific
  naming anymore - it's general-purpose Aspire/identity naming only.
- `src/EShop.AppHost/AppHost.cs` hosts: a SQL Server database for the Seller Portal
  (ADR 0003; a real Azure SQL Database once deployed, ADR 0019), Azure Service Bus
  messaging for the Seller Portal ↔ Commerce Connect integration (ADR 0009), Azure Blob
  Storage claim-check storage for submission images (ADR 0009), Redis for the Seller
  Portal's auth ticket store and Data Protection key ring (a real Azure Cache for Redis
  once deployed, ADR 0021, superseding ADR 0012's Valkey), and Keycloak for local
  development only (ADR 0010; Microsoft Entra External ID once deployed, ADR 0020). The
  Storefront search resource will sit behind a search microservice, with Elasticsearch
  locally and Azure AI Search in a deployed instance (ADR 0022 and ADR 0024), once the
  Storefront/search slice is scaffolded. All but Keycloak are persistent
  (`WithLifetime(ContainerLifetime.Persistent)`); Keycloak has no data volume,
  so every `aspire start` gets a fully fresh container,
  pre-configured from `src/EShop.AppHost/Realms/eshop-realm.json`, which now seeds every
  non-Shopper actor from `SPEC.md` (`test-seller`, `test-content-editor`, `test-marketer`,
  `test-merchandiser`, `test-customer-service-agent`, `test-site-administrator`).
  `KeycloakStorefrontClientProvisioner` assigns the Storefront staff realm roles
  (`ContentEditor`/`Marketer`/`Merchandiser`/`SiteAdministrator`/`CustomerServiceAgent`)
  to the matching test users at provisioning time - see the `EShop.StoreFront.Web`
  bullet below. An Azure Container Apps environment resource
  (`AddAzureContainerAppEnvironment`, ADR 0018) is added only when publishing.
- `test/EShop.AppHost.Tests` (xUnit 3, `Aspire.Hosting.Testing`) asserts these
  resources are registered in the AppHost's app model, with no containers started.
  `test/EShop.AppHost.E2ETests` starts the real AppHost and drives headless Chromium
  against it via `Microsoft.Playwright` (ADR 0011) - the Seller Portal login flow, the
  full Movie/Merchandise draft submission loop
  (`SellerPortalMovieDraftSubmissionTests`, `SellerPortalMerchandiseDraftSubmissionTests`),
  and the Seller Draft Approval Simulator's SignalR live-push behavior
  (`SellerDraftApprovalSimulatorLivePushTests`).
  The Storefront's own tests will join it once that app exists. Deliberately not part
  of `EShop.slnx` - much slower, needs Docker - so it only runs via `eshop test e2e`,
  which always forces container execution regardless of `--tools`.
- `doc/c4/` has a System Context diagram, a Container diagram, and one Component
  diagram (the Seller submission/review mechanism, per ADR 0008/0009), each a `.puml`
  source rendered to `.svg` via `eshop diagram render`. Other component diagrams are
  deferred — see `doc/TODO.md`.
- `EShop.Cli` (ADR 0016; Linux only, for now - ADR 0017) is the one entry point for
  restore/build/format/test (unit, coverage, e2e)/run/diagram render/screenshot/
  generate-types, started via `scripts/eshop.sh` - the only script left in `scripts/`,
  alongside `Containerfile` and `container.env`. `IToolExecutor` runs each tool locally
  or inside the `eshop-utility` Containerfile image, per `--tools auto` (local-first,
  the default), `local`, or `container`. `eshop build`/`restore`/`test` (unit) run their
  independent diagnostic-gathering steps concurrently (`Task.WhenAll`); `test
  e2e`/`test coverage` stay sequential, since each step there depends on the previous
  one's output. `HumanOutputSink` shows a live spinner per in-flight step;
  `AiOutputSink` (`--agent`) writes a once-a-minute liveness heartbeat while a step is
  open, and scopes `NO_COLOR` to itself only - human-mode output keeps real color.
  `EShop.Cli.Tests` covers it, with Docker/real-CLI-backed tests isolated under
  `Integration/` (`[Trait("Category", "Integration")]`, excluded from the default run).
- The Seller Portal implements both Movie and Merchandise draft flows from
  `doc/SPEC.md` end-to-end: create, edit, delete, and submit for review. The BFF
  issues and enforces its own antiforgery token on every mutating route. A
  submission's outcome (`Approved` with an assigned SKU, or `Rejected` with a reason)
  stays visible on its own history view even after the originating Draft is gone.
  `src/EShop.SellerPortal.Web` uses Tailwind CSS v4 and shadcn/ui components.
- The Seller Portal/Commerce Connect message contract (`src/EShop.Messaging`,
  explicitly PROVISIONAL per ADR 0009) is kind-agnostic: one `SubmissionRequestMessage`
  carries either a Movie or a Merchandise submission's fields (movie-only fields null
  for a Merchandise submission, and vice versa), with a list of image blob references.
  The Service Bus consumer base class and these message types live in
  `src/EShop.Messaging`, not the Bff, so a second project (`EShop.DevTools`) can reuse
  them without depending on the Bff or pulling a Service Bus SDK dependency into
  `EShop.Common`. That base class runs in-process inside the hosting app rather than as
  a separate worker project - an accepted trade-off for this demo's message volume.
- Commerce Connect (the Merchandiser-review counterpart to the Seller Portal's
  submission flow) has not been scaffolded yet. `src/EShop.DevTools` is a dev-only
  Razor Pages site (gated out of a real deployment by Aspire's publish-mode check)
  where a developer can inspect and drive workflow states. Some DevTools flows are not
  meant for Merchandisers, and some cannot be expressed through CMS approval sequences.
  Its
  "Seller Draft Approval Simulator" page updates live via `SellerSubmissionsHub`
  (SignalR, ADR 0015): every open tab does a full page reload on a
  "submissionsChanged" event, the same pattern Approve/Reject already use.
- The Seller inventory slice (ADR 0007) is now built and wired end-to-end: a Seller
  reports a quantity for one of their own approved SKUs from the Web's `/inventory`
  page; the Bff validates ownership, upserts a `SellerInventory` row, and publishes an
  inventory-report message; `EShop.DevTools`' Inventory Report Simulator page publishes
  back a Confirmed or Failed result; a Bff-side consumer updates the row's sync status
  accordingly, which the `/inventory` page then reflects.
- The Seller Portal shows toast notifications: an immediate one on save/submit, and a
  pushed one when a submission is approved or rejected while the Seller is anywhere in
  the authenticated portal. The push side is Server-Sent Events - a single-process,
  in-memory `SubmissionNotificationBroadcaster` in the BFF fans a submission's outcome
  out to that Seller's open SSE connection(s), if any (a Seller with none simply misses
  the push; `GET /bff/api/submissions` stays independently authoritative).
- The BFF's ASP.NET Core Data Protection key ring persists into Redis
  (`RedisXmlRepository`, `Program.cs`), so the `seller-portal`/`XSRF-TOKEN` cookies
  survive an `aspire start` restart - no manual cookie-clearing needed.
  `AntiforgeryEndpointFilter`/`lib/bff-fetch.ts` also self-heal a stale antiforgery
  pairing (refresh and retry once) for the one gap persistence alone doesn't close.
- The Bff's `Contracts/*.cs` are the source of truth for the data contracts
  `lib/types.ts` used to define by hand (ADR 0014). The Bff generates an OpenAPI
  document at build time; `eshop generate types` turns it into a committed
  `lib/api-schema.d.ts` via `openapi-typescript`, and `lib/types.ts` only re-exports
  from that file. `eshop build` fails when the committed file goes stale; `eshop
  format` regenerates it. The Contracts' status/kind/format properties carry the real
  C# enum (a global `JsonStringEnumConverter` keeps the JSON wire format unchanged),
  so the generated TypeScript type keeps a specific string-literal union.
- `aspire init` has been run; agent skills and MCP configuration were added locally and
  are gitignored (see `README.md` "Agent coding harness" section).
- Per ADR 0018, `AppHost.cs` no longer chains `.WaitFor(...)` onto the Bff's
  dependencies (or DevTools'). Each dependency's resilience is now the consuming
  service's own concern instead:
  - The Bff's own `/health` (via `.WithHttpHealthCheck("/health")` on its AppHost
    registration) aggregates each Aspire client integration's own auto-registered
    health check (SQL, Service Bus, Blob Storage, Redis), so it's a single accurate
    readiness signal for both the local orchestrator and a deployed instance's
    readiness probe. `EShop.ServiceDefaults`' `MapDefaultEndpoints` now maps `/health`/
    `/alive` in every environment, not just Development, with request-timeout/
    output-cache hardening.
  - `ServiceBusQueueConsumer` (`src/EShop.Messaging`, shared by the Bff and DevTools)
    wraps its initial `StartProcessingAsync` call in a Polly retry pipeline
    (`Microsoft.Extensions.Resilience`), so a not-yet-reachable Service Bus namespace
    retries instead of crashing the whole host.
  - The Seller Portal database now follows ADR 0023's migration model: a dedicated
    `EShop.SellerPortal.MigrationRunner` project (a plain console AppHost resource, no
    HTTP endpoint) applies pending EF Core migrations programmatically
    (`Database.MigrateAsync()`, not `dotnet ef database update` - see `doc/CHRONICLE.md`
    for why `Aspire.Hosting.EntityFrameworkCore`'s built-in `AddEFMigrations` couldn't do
    this), holding a `sp_getapplock`-based exclusive lock (`EShop.Migrations.Orchestration`'
    `SchemaMigrationLock`, keyed on `KnownNames.MigrationsSellerPortalLockName`) on the
    same connection for the whole operation, and writing a `SchemaMigrationMarkers` row
    (`SchemaMarkerStore`) recording the outcome. The Bff no longer migrates its own
    database at startup; its `SchemaMarkerHealthCheck` (part of `/health`, not `/alive`)
    reports not-ready until that marker shows the current migration succeeded. Both the
    runner's initial connection-open and its migration call are wrapped in
    `SqlExceptionRetry` (moved out of the Bff's old `DbContextConfiguration.cs`, now
    shared), since a cold-starting SQL Server can reset either one - this is also why
    the health check itself must catch a `SqlException` and report Unhealthy rather than
    throw. Deployment automation (publishing this runner as an Azure Container Apps job
    that gates the Bff's rollout) and extending the same model to the Storefront's
    databases are still open - `doc/TODO.md` tracks both.
  - The cache resource needed no such treatment: Aspire's `AddRedisClient` already sets
    `AbortOnConnectFail = false` by default, and (per ADR 0021) both local development
    and a deployed instance now use the same `AddAzureManagedRedis(...).RunAsContainer()`
    hybrid resource - no branching, same as SQL Server/Azure SQL Database above.
  - **`EShop.StoreFront.MigrationRunner` extends ADR 0023 to Optimizely CMS/Commerce -
    fully implemented, wired, and verified end to end, including against the real
    Optimizely scripts (not just synthetic fixtures) and inside a real `eshop run`
    session.** One binary, selected by a `cms`/`commerce` argument, applies Optimizely's
    own shipped SQL scripts (`EShop.Migrations.Optimizely`'s `OptimizelySqlScriptRunner`/
    `OptimizelyPackageScriptLocator`, reimplementing `EPiServer.Net.Cli`'s own algorithm -
    see `doc/CHRONICLE.md`) rather than an EF Core migration, reusing
    `EShop.Migrations.Orchestration`'s lock/marker/retry unchanged. `CopyOptimizelySchemaScripts` (an
    MSBuild target in the runner's own `.csproj`) copies the real `episerver.cms.core`/
    `episerver.commerce.core` `tools/` scripts into its build output; a real run against
    a fresh, empty SQL Server applied the full real schema (80 real CMS tables, 145 real
    Commerce tables) and no-opped cleanly on a second run. Wired into `AppHost.cs`: the
    CMS/Commerce SQL databases plus two runner resource instances
    (`.WithArgs("cms")`/`.WithArgs("commerce")`), no `.WaitFor` on either (see the
    `EShop.StoreFront.Web` bullet below for why).
  - **`nuget.config` now has a second source**: Optimizely's own feed
    (`https://nuget.optimizely.com/feed/packages.svc/`), scoped via package source
    mapping to `EPiServer`/`EPiServer.*`/`Optimizely.*` only - every other package still
    resolves from `nuget.org` exactly as before.
  - **`EShop.StoreFront.Web` is scaffolded, wired into `AppHost.cs`, and verified to
    start end to end in a real `eshop run` session** (from `epi-commerce-empty`, upgraded
    to .NET 10/CMS `13.0.2`/Commerce Connect `15.1.0` - see `doc/CHRONICLE.md` for why
    those exact versions, not the newer `13.1.1`, and for two extra required package
    references neither meta-package pulls in on its own). Uses the classic
    `Startup.cs` hosting model, not this repo's usual minimal-hosting pattern - verified
    empirically that `AddCms()`/`AddCommerce()` do not work under
    `WebApplication.CreateBuilder` (see `doc/CHRONICLE.md`); `EShop.ServiceDefaults` was
    refactored to support both models from one implementation rather than duplicate it.
    `AddCmsAspNetIdentity` (the template's own default) is dropped, not adapted - ADR
    0002 already makes external OIDC the identity system of record, and that package
    doesn't exist for CMS 13 anyway. `DataAccessOptions.UpdateDatabaseSchema`/
    `CreateDatabaseSchema` are both explicitly `false`, plus a blocking `ISchemaValidator`
    (ADR 0023 - normal startup must never mutate schema). Readiness is
    `StorefrontMigrationPreflight.cs`, not `.WaitFor` or a health check: Optimizely's own
    `DatabaseSchemaHost` hosted service crashes the whole app at startup on a missing
    schema (no EF-Core-like "start anyway, report Unhealthy" option), and `.WaitFor` has
    no effect once deployed to Azure Container Apps, so `Program.cs` checks
    `SchemaMarkerStore` for both components itself, before ever building the real host -
    pure in-process C#, so it behaves the same regardless of orchestrator. Verified live:
    started against unmigrated databases (it polled without crashing), ran both
    migrations while it waited, watched it detect completion and proceed to a full,
    successful startup - real CMS/Commerce content types created, "Application started."
    `EPiServer.Data`'s connection-string convention needs exactly `EPiServerDB`/
    `EcfSqlConnection` (not this repo's usual kebab-case Aspire resource names), bridged
    in `AppHost.cs` via `.WithEnvironment("ConnectionStrings__EPiServerDB", ...)` rather
    than `.WithReference`. Still has no content types - that's a later phase.
  - **Storefront authentication foundation phase is done.**
    `EShop.StoreFront.Web/Authentication/StorefrontAuthConfiguration.cs` builds its own
    dual-provider OIDC wiring (Keycloak locally / Microsoft Entra External ID once
    deployed), mirroring the Bff's `AuthConfiguration.cs` pattern, plus `/login`,
    `/logout`, `/user` minimal endpoints (`AuthEndpoints.cs`, unprefixed - Storefront
    isn't proxied behind a separate frontend the way the Bff is). Closes a real gap:
    this project called neither `AddCmsAspNetIdentity()` nor `EPiServer.OptimizelyIdentity`'s
    `AddOptimizelyIdentity()`, so Optimizely's own CMS user/role sync never ran - fixed
    by calling the public `EPiServer.Security.ISynchronizingUserService.SynchronizeAsync`
    on sign-in ourselves. External roles map onto Optimizely's role vocabulary via the
    public `AddMappedRole` virtual-role API (`ContentEditor`→`CmsEditors`,
    `SiteAdministrator`→`CmsAdmins`, `Merchandiser`→`CatalogManagers` and a reserved,
    not-yet-consumed custom `Merchandisers` role for a future review tool).
    `KeycloakStorefrontClientProvisioner` (+ a new shared `KeycloakAdminApiClient`,
    extracted from `KeycloakSellerPortalClientProvisioner` to avoid duplicating the
    token/role/scope/client mechanics) provisions the Storefront's own Keycloak client
    and realm-role assignments. `AppHost.cs` wires both the Keycloak and Entra branches;
    `appsettings.Development.json` needed a `storefront-oidc-client-secret` default
    alongside the existing `seller-portal-oidc-client-secret`/`keycloak-admin-password`
    ones (missing it the first time caused a real `MissingParameterValueException`).
    Verified: `eshop build`/`eshop test` clean, including new/updated
    `EShop.AppHost.Tests` resource-model assertions. **Not yet verified via the new
    `StorefrontLoginTests.cs` e2e test** - currently blocked by a pre-existing,
    unrelated containerized-e2e build issue. Later phases
    (a custom Optimizely Shell tool for Merchandiser submission review - native
    approval has no step for assigning an existing SKU before approving/rejecting, so
    the stock Content Approvals list can't be reused as the review UI - then the
    Commerce/CMS content types and Storefront shell pages) are identified but not yet
    started.

## Conventions and Constraints

- To add a new dev tool to `EShop.DevTools`, create a folder under
  `Pages/Tools/<Name>/`, link it from `Pages/Index.cshtml`, and add a nav entry to
  `Pages/Shared/_Layout.cshtml` - there is no config-driven tool registry.

## Non-obvious Current Constraints

Surprising or easy-to-break rules future work must not accidentally violate. See
`doc/CHRONICLE.md` for the reasoning/incident behind each, linked where noted.

- `AppHost.cs` provisions the Seller Portal's OIDC `seller-portal` Keycloak client via
  a `sellerPortalWeb.OnResourceReady(...)` subscription, not
  `OnResourceEndpointsAllocatedEvent` - the latter would delay every other executable
  resource's startup, not just this one's.
- No `secret: true` `AppHost.cs` parameter (`keycloakAdminPassword`,
  `sellerPortalOidcClientSecret`, `storefrontOidcClientSecret`, and - publish-mode only,
  no Development default at all - `entraClientSecret`/`storefrontEntraClientId`/
  `storefrontEntraClientSecret`) may get a literal default in code; a Development-only
  value belongs in that project's own `appsettings.Development.json` only. Forgetting
  one throws `Aspire.Hosting.MissingParameterValueException` at AppHost startup, not at
  build time - easy to miss until something actually tries to run.
- `Aspire.Hosting.Keycloak` has never published a stable (non-preview) release, even
  though Aspire itself is stable - check before bumping its pinned version.
- `Directory.Packages.props`'s `Microsoft.AspNetCore.DataProtection.StackExchangeRedis`
  (pinned to 10.0.10) and `SQLitePCLRaw.bundle_e_sqlite3` (pinned to 2.1.12) are each
  deliberately newer than ADR 0013's 40-day quarantine would otherwise allow - each is a
  documented security exception (the "compliant," older version has a disclosed
  high-severity CVE), not an inconsistency to fix by aligning them with nearby pins at
  the next quarantine review.
- `eshop test e2e`/`eshop run` refuse to start while a native AppHost session for this
  project is already up (`IAppHostGuard`) - running two at once corrupts
  `node_modules` (both write to the same bind-mounted folder). The guard has a known
  gap: it can't see a running session on the *other* side (native vs. containerized).
- `src/EShop.SellerPortal.Web/next.config.ts`'s `NEXT_DIST_DIR`-driven `distDir` must
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
- `EShop.DevTools`' SignalR hubs (`SellerSubmissionsHub`, `SellerInventoryHub`) and Razor
  Pages have no authentication - acceptable only because the project is dev-only tooling
  excluded from a real deployment.

## Open Follow-ups

- Storefront auth foundation phase is done - see Repository
  state above. Its later phases are identified but not started: a custom Optimizely
  Shell tool for Merchandiser submission review (amends ADR 0008 - native approval
  alone can't handle SKU deduplication before approve/reject), then the Commerce/CMS
  content types (MovieProduct/MovieVariant, MerchandiseProduct/MerchandiseVariant -
  checkout content types stay deferred) and Storefront shell pages. This also resolves
  the direction (though not yet the implementation) of `doc/TODO.md`'s "Optimizely
  native-approval/external-OIDC compatibility" question.
- `StorefrontLoginTests.cs` (new e2e test, `test/EShop.AppHost.E2ETests`) has not been
  run successfully yet - blocked by a pre-existing, unrelated bug in `eshop test e2e`'s
  containerized build of `EShop.StoreFront.MigrationRunner`; fix that before trying this
  test again.
- Scaffold the search microservice and the Profile microservice.
- Extend `eshop build`/`restore`/`format` coverage notes if `EShop.StoreFront.Web` ever
  gains its own frontend build step (it has none yet - no separate JS/TS build).
- Add the still-missing Azure Container Apps job publication/deployment gating for all
  three migration runners (Seller Portal's and both Storefront ones).
- The two Storefront migration runner resources' `ToolsDirectory`/script-folder-name
  configuration (`appsettings.json` in `EShop.StoreFront.MigrationRunner`) is still a
  literal, separate from `Directory.Packages.props`'s pin - only the version number
  itself is derived automatically now (`OptimizelyInstalledVersion`, reading the actual
  referenced assembly rather than a duplicated literal - see `doc/CHRONICLE.md`).
- Add the search microservice first with a basic API and health checks. Defer indexing
  and query endpoints until the Storefront search document shape is decided.
- See `doc/TODO.md` for the rest of the open and deferred questions (the
  concurrent-multi-replica-migration risk, the Entra External ID setup steps, and the
  remaining C4 component diagrams to add) and `ISSUES.md` for open infrastructure
  defects.
