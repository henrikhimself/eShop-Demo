# Memory

## Current State

- eShop is a .NET 10/Aspire/Optimizely CMS 13 and Commerce Connect 15 movie-store demo.
  It sells movie formats and merchandise through a marketplace with third-party Sellers.
  `SPEC.md` is the domain source of truth.
- AppHost provides local SQL Server, Service Bus, Blob Storage, Redis, Keycloak, and the
  development reverse proxy. A deployed instance uses Azure Container Apps, Azure SQL,
  Azure Cache for Redis, and Microsoft Entra External ID. See `System landscape.md` and
  the ADRs for boundaries and deployment decisions.
- Database schema changes run only through explicit migration resources. Applications do
  not change schemas at startup; they use schema markers and readiness checks instead.
- The Seller Portal implements Movie and Merchandise draft flows, inventory reporting,
  antiforgery protection, durable submission/inventory messaging, and SSE notifications.
  Its Service Bus message contracts remain provisional until a real Commerce Connect
  consumer exists.
- Storefront is scaffolded with classic `Startup.cs` hosting. It uses explicit Optimizely
  SQL-script migration resources and an external OIDC identity provider. Catalog content
  types, the Profile microservice, and the search microservice are not implemented.
- ADR 0029 selects a private Storefront AI bridge for Anthropic Claude Sonnet 5.5. It will
  run locally and as an internal Azure Container App, with full CMS tool-enabled chat.
  `EShop.StoreFront.EpicwebAI.Cms` owns the Epicweb CMS package dependencies and the
  fixed tool and hub registrations. The Storefront consumes them through one project
  reference.
  The AppHost user-secrets store supplies the local Anthropic key. Deployment secret
  provisioning and rotation remain open work in `TODO.md`. Claude access is limited to
  the WebEditors, CmsEditors, WebAdmins, and CmsAdmins roles. No user receives the
  Epicweb AIEditors role. All Storefront data can go to Anthropic. Image generation and
  vision are not supported. The bridge uses the shared cache only for short-lived Claude
  tool-call correlation state; it removes the state after the matching tool result.
- Local Keycloak has separate `seller` and `storefront` realms. They use different
  issuers and browser sessions, so local cross-application single sign-on is intentional
  not supported. Production still uses Microsoft Entra External ID.
- DevTools is development-only workflow tooling. It is excluded from a deployed instance.
  It holds a short-lived in-memory cancellation tombstone so independent cancellation and
  submission queues cannot display a cancellation-first submission as pending.
- `EShop.Cli` is the Linux-only developer command entry point. Normal tests run through
  `eshop test`; containerized browser E2E tests run only through `eshop test e2e` and
  start a complete AppHost one test at a time.

## Conventions and Constraints

- Code is authoritative for literal resource names, paths, keys, versions, and signatures.
  Architecture documents state decisions and link to code instead of duplicating literals.
- A project that directly consumes a non-free component must be under `src/nonfree` and
  its README must identify the paid product and the dependency, feature, or service that
  makes it a paid-product consumer. A transitive project reference does not trigger this
  rule (ADR 0029).
- Use `scripts/eshop.sh <command> --agent` for repository workflows. The CLI selects local
  or utility-container tools; `--tools local` never falls back to the container.
- Add DevTools pages under `Pages/Tools/<Name>/`, then link them from both the index and
  layout. There is no configuration-driven tool registry.
- Keep `MEMORY.md` current-state only. Put open work in `TODO.md`, decisions in ADRs, and
  historical reasoning in `CHRONICLE.md`.
- The `lib/DotNet-ReverseProxy` submodule is independently owned. Read its instructions
  before changing or testing it.

## Non-obvious Current Constraints

- Secret AppHost parameters have no code defaults. Development-only values belong in the
  owning project's `appsettings.Development.json`; otherwise AppHost startup fails.
- `CopyOptimizelySchemaScripts.targets` must ensure a trailing slash on
  `$(NuGetPackageRoot)`: the container's `NUGET_PACKAGES` path has none.
- `Aspire.Hosting.Keycloak` remains preview-only. Check its release status before changing
  the pinned version.
- Do not run `eshop run` or `eshop test e2e` beside a native AppHost session. The guard
  prevents same-mode overlap, but cannot detect native/containerized cross-mode overlap.
- `NEXT_DIST_DIR` must keep native and containerized Next.js builds in separate caches.
- Run `eshop build` before `eshop run`, and use the same native or containerized mode for
  both. Switching modes requires a rebuild because AppHost embeds an absolute project path.
- During build-time OpenAPI generation, the BFF registers non-connecting stand-ins for
  infrastructure services so endpoint generation can complete without AppHost resources.
- Keep `builder.ExecutionContext.IsPublishMode` in `AppHost.cs`; moving it to
  `EShop.Common` would bring AppHost orchestration binaries into every dependent project.
- `eshop test e2e` and `eshop screenshot` always use the utility container because it owns
  Chromium. Screenshot login requires caller-supplied same-origin login path and credentials.
- DevTools has no authentication because it is development-only and excluded from deployment.
- Storefront claim-name mapping must be registered during service registration, before DI
  is built.
- Refresh-token exchanges use Redis-backed coordination per application and token hash.
  A Redis coordination failure rejects the session to retain strict provider confirmation.

## Open Follow-ups

- See `TODO.md` for active product, deployment, migration, search, Profile, and reverse-proxy
  follow-ups.
- `TODO.md` tracks centralizing the Storefront migration runners' script-directory settings.
