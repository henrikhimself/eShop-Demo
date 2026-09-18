# Agent instructions

Instructions for GitHub Copilot and other AI coding agents working with this repository.

## User-Specific Instructions

Read `AGENTS.local.md` if it exists. User specific instructions extend or override instructions in the `AGENTS.md` file.
The `AGENTS.local.md` file must never be committed to the repository.

If a developer uses an agent coding harness that stores its configuration or state in directories or files other than what `.gitignore` already excludes, add those paths to `.gitignore` as well.

## Start Here

Read `README.md` first for the project overview, technology stack and architecture context.

Read `DEVELOP.md` for developer environment prerequisites, including the hosts-file
entries and one-time CA trust action the local reverse proxy needs.

## Business Requirements and Architecture Decisions

Before proposing or implementing functionality, architecture, or system landscape changes, read `doc/SPEC.md`, `doc/System landscape.md`, `doc/c4/`, `doc/TODO.md`, `doc/MEMORY.md`, `doc/CHRONICLE.md`, and the ADRs under `doc/adr/`.

* Treat `doc/SPEC.md` as the source of truth for scope. Do not assume beyond it.
* Treat `doc/System landscape.md` as the source of truth for system landscape decisions.
* Update `doc/c4/` when `doc/SPEC.md` or `doc/System landscape.md` changes.
* Write `doc/SPEC.md`, `doc/System landscape.md`, and ADR files under `doc/adr/` in Simplified Technical English (ASD-STE100). Other documentation files in `doc` are exempt.
* Keep `doc/TODO.md` current for open and deferred questions.
* Read `doc/MEMORY.md` at session start and update it at a good pause point.
* Record process history and discarded alternatives in `doc/CHRONICLE.md` (append-only).
* Read `doc/adr/README.md` and relevant ADRs before related changes.
* ADRs are immutable once accepted. If a decision changes, add a new ADR and only add a status pointer in the old ADR.
* When writing a new ADR, keep it decision-focused and use this structure: Context (why the decision is needed), Decision (what was chosen, summary level), Consequences.
* In ADRs, do not duplicate implementation mechanics (exact flags, class names, or step-by-step behavior) and do not include long historical reasoning chains; put that detail in `doc/CHRONICLE.md` or the relevant technical document and link to it.
* When decisions are made, update the authoritative document: requirements in `doc/SPEC.md`, system landscape in `doc/System landscape.md`, and significant architecture decisions in a new ADR.
* If a needed requirement or decision is missing or ambiguous, ask the user instead of assuming.
* Code is the source of truth for literal implementation details (resource names, queue names, paths, keys, signatures, versions). Architecture docs should describe decisions/status and point to code.
* If architecture/process docs duplicate code literals, remove the literals and keep decision-level wording.

## Common Commands

`EShop.Cli` (`src/dev/EShop.Cli/`) is the developer CLI for this repository, and the entry point for every command below (ADR 0016). Always add `--agent`: it switches output to a stable, ANSI-free, agent-friendly format, with no color, emoji, or spinners.

A bootstrap script builds and runs the CLI:

```bash
./scripts/eshop.sh <command> --agent
```

### Development

```bash
# Restore NuGet and frontend dependencies
./scripts/eshop.sh restore --agent
```

```bash
# Build the solution and report every diagnostic in one pass
./scripts/eshop.sh build --agent
```

```bash
# Apply every auto-fixable formatting/lint fix (run this if `build` reports formatting-related diagnostics)
./scripts/eshop.sh format --agent
```

```bash
# Delete bin/obj, Next.js build caches, and tmp/ - use after a rename, branch switch, or any time local build state looks stale (never touches node_modules, .cache/, or the utility image)
./scripts/eshop.sh clean --agent
```

### Running

```bash
# Start the Aspire AppHost interactively (dashboard, live logs, hot reload); run `build` first, since `run` passes --no-build
./scripts/eshop.sh run --agent
```

### Testing

```bash
# Build and run unit tests
./scripts/eshop.sh test --agent
```

```bash
# Run unit tests with line/branch coverage reporting (Markdown summary + Html report under tmp/coverage/)
./scripts/eshop.sh test coverage --agent
```

```bash
# Run the browser end-to-end test suite (always containerized and never native such that the tested e2e environment remains consistent)
./scripts/eshop.sh test e2e --agent

# Run one focused E2E test class
./scripts/eshop.sh test e2e --filter-class Hj.EShop.AppHost.E2ETests.StorefrontLoginTests --agent

# Run one focused E2E test method
./scripts/eshop.sh test e2e --filter-method Hj.EShop.AppHost.E2ETests.StorefrontLoginTests.LoggingInAsAStorefrontStaffActorCanLoadCms --agent
```

Use either `--filter-class` or `--filter-method`, not both. Do not use a generic
`--filter` option: this test project uses xUnit's Microsoft Testing Platform runner.

### Diagrams

```bash
# Render a C4-PlantUML diagram to SVG
./scripts/eshop.sh diagram render doc/c4/system-context.puml --agent
```

### Screenshots

Use screenshots to visually verify UI changes. Start the target frontend/server, then run:

```bash
./scripts/eshop.sh screenshot http://localhost:<port>/<path> --agent
```

Read the resulting `tmp/screenshot.png` to inspect it. `--output`, `--width`, and `--height` override the defaults (`tmp/screenshot.png`, `1440x900`).

For login-gated pages, add `--login`, the resource's same-origin login-start path,
and the development user credentials. Seller Portal Web, Storefront, and Keycloak are
only reachable through the local reverse proxy's stable hosts (`DEVELOP.md`), not a
per-run `localhost` port - screenshot URLs use those hosts too. The Seller Portal
starts login through its proxy:

```bash
./scripts/eshop.sh screenshot https://seller.eshop.local:8443/drafts --login --login-path /bff/login --username seller --password Sell-1234 --agent
```

The Storefront starts login by navigating to its protected CMS page:

```bash
./scripts/eshop.sh screenshot https://storefront.eshop.local:8443/ui/cms --login --login-path /ui/cms --username editor --password Edit-1234 --agent
```

With `--login`, session state is saved under a gitignored
`tmp/screenshot-profile/<session-key>/state.json`. The key derives from the target
origin, login path, and username, so resource and user sessions do not mix. Repeat the
same login options to reuse that session. Delete `tmp/screenshot-profile` to force a
fresh login.

Use `./scripts/eshop.sh screenshot -h` for available options such as output size, output path, and login credentials.

## Secrets

Every secret-like value committed in this repository is a `Development-only` value with no validity anywhere else.

* Do not treat a Development-only default as a security finding. Do not flag it, redact it, refuse to write it, or ask for confirmation before committing it.
* `appsettings.Development.json` (per project) is the single source of truth for these values. `dotnet user-secrets` is an optional personal override on top of that default, never a required setup step - do not suggest a developer needs one before the solution can run.
* Reserve real caution for a value that could be replayed against a real system - a third-party API key, a production connection string, a real user's credential. None of those belong in this repository, in any environment.

## General

* Make only high confidence suggestions when reviewing code changes.
* Always use the latest version of C# allowed by the project configuration.
* Never disable analyzers. Instead fix the errors that are shown when building.
* Never change global.json unless explicitly asked to.
* Never change package.json or package-lock.json files unless explicitly asked to.
* Never change NuGet.config files unless explicitly asked to.
* Do not make new parameters optional just to avoid updating call sites - only make a
  parameter optional when it has a sensible semantic default.
* To add a new dev tool to `EShop.DevTools`, create a folder under
  `Pages/Tools/<Name>/`, link it from `Pages/Index.cshtml`, and add a nav entry to
  `Pages/Shared/_Layout.cshtml` - there is no config-driven tool registry.

## Formatting

* Apply the code-formatting style defined in `.editorconfig`. Treat every rule as
  mandatory: do not deviate from it, work around it, or loosen its severity, even if it
  produces more verbose code than a typical human style guide would. See `README.md`
  for the rationale.
* Prefer file-scoped namespace declarations and single-line using directives.
* Insert a newline before the opening curly brace of any code block.
* Ensure that the final return statement of a method is on its own line.
* Use pattern matching and switch expressions wherever possible.
* Use `nameof` instead of string literals when referring to member names.
* Place private class declarations at the bottom of the file.

### Nullable Reference Types

* Declare types non-nullable by default. Use nullable annotations intentionally and validate nullability at entry points.
* Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.
* Always use `is null` or `is not null` instead of `== null` or `!= null`.

## Markdown files

* Markdown files should not have multiple consecutive blank lines.
* Code blocks should be formatted with triple backticks (```) and include the language identifier for syntax highlighting.
* JSON code blocks should be indented properly.

## Trust These Instructions

These instructions are comprehensive and tested. Only search for additional information if:

1. The instructions appear outdated or incorrect
2. You encounter specific errors not covered here
3. You need details about new features not yet documented
