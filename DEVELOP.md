# Developing eShop Demo

`README.md` describes what this project is. This file describes how to set up a
machine to build, run, and test it.

## Prerequisites

- x86-64 (amd64) CPU
- Docker

## Prerequisites for running local Aspire CLI

- .NET 10
- Node
- Corepack pnpm
- Aspire CLI compatible with the `Aspire.AppHost.Sdk` version

Use the versions pinned by `global.json`, `.nvmrc`, and `package.json`.

### Hosts file entries

The local development reverse proxy gives the Seller Portal, the Storefront, and
Keycloak their own stable browser host names. Add these entries to `/etc/hosts` before
running the solution:

```text
127.0.0.1 seller.eshop.local storefront.eshop.local identity.eshop.local
::1       seller.eshop.local storefront.eshop.local identity.eshop.local
```

See `doc/System landscape.md` ("Local development ingress") for why these three names
exist and what each one routes to.

### One-time reverse-proxy CA trust

The reverse proxy presents a certificate signed by a self-signed local root CA. A
browser must trust that CA once before it will accept `https://*.eshop.local:8443`
without a warning.

The first `aspire start`/`eshop run` generates the CA under the `REVERSEPROXY_HOME`
directory if it does not already exist:

- Native execution: `~/.reverseproxy`
- Containerized execution (`eshop run` fallback): the repo-relative
  `.cache/home/.reverseproxy`

Each mode generates its own, independent CA - trust the CA for each mode you actually
use.

Trust the CA in the system-wide store (Debian/Ubuntu-style):

```bash
sudo cp <REVERSEPROXY_HOME>/ReverseProxy-RootCA.crt.pem \
  /usr/local/share/ca-certificates/eshop-reverse-proxy-ca.crt
sudo update-ca-certificates
```

Replace `<REVERSEPROXY_HOME>` with the directory for the mode you are trusting (see
above).

Some browsers (for example, the Chrome/Chromium family) do not always honor the system
trust store for HTTPS on every Linux distribution. If a browser still shows a
certificate warning after this step, see `lib/DotNet-ReverseProxy`'s own troubleshooting
documentation for that browser's own trust-store import - this document does not
duplicate unverified per-browser steps.

## Failure behavior

- **Missing hosts entries**: the browser fails with a DNS/connection error (for
  example, "This site can't be reached") before the request ever reaches the reverse
  proxy. Fix: add the hosts entries above.
- **Untrusted CA**: the browser shows a TLS/certificate warning for
  `https://*.eshop.local:8443`. Fix: the one-time CA trust action above; repeat it for
  each execution mode (native, containerized) you use.
- **`REVERSEPROXY_HOME` unwritable or inaccessible**: the AppHost itself fails to start
  with `InvalidOperationException: Cannot create or access the reverse-proxy CA
  directory '<path>'.` Fix: check permissions on that directory, or set the
  `REVERSEPROXY_HOME` environment variable to a writable location.
- **Unrecognized or misspelled host through the proxy**: the proxy returns no route
  (HTTP 404). This is expected behavior, not a bug - the proxy only routes the three
  canonical hosts documented in `doc/System landscape.md`.
- **An edit to `src/EShop.AppHost/Realms/eshop-realm.json` does not take effect**: the
  Keycloak container now stays running (and keeps its own database) across an ordinary
  `aspire start`/`eshop run` restart, so Keycloak's own realm import - which only
  imports a realm that does not already exist - skips your edit. Fix: stop `aspire
  start`/`eshop run`, remove the `keycloak` container (`docker rm -f`, or your
  container tool's equivalent), then start again for a clean import.

## Developer CLI

The `./scripts/eshop.sh` script installs and exposes commands for building, testing, and managing the development environment. Run `./scripts/eshop.sh -h` for usage instructions.

The eShop CLI formalizes development workflows, automates routine tasks, and enforces strict quality gates to ensure consistency and reliability.

It provides an AI-friendly developer experience with terminal output designed to be easily understood by AI coding agents. It also acts as a guardrail against common AI coding failure modes, helping detect when agents take shortcuts, skip validation, or otherwise drift from the project's required quality standards.

When `--tools local` is used, or `--tools auto` selects a local tool, the CLI sets the
same repository-relative `.cache` locations used by the utility container for the tool
home, NuGet packages, npm cache, and pnpm data. This keeps re-fetchable tool state
inside the checkout and out of the developer's normal home directory.

See `AGENTS.md` ("Common Commands") for the exact commands to run for restore, build, format, test, run, diagrams, and screenshots.

### Troubleshooting command steps

Add `--debug` to stream each utility's standard output and standard error while it
runs. Every line includes the utility name and output stream. Debug output has no
spinner, color, or rich terminal widgets, so it is suitable for diagnosing a command
step that appears to stop responding.

For example, to inspect frontend dependency installation:

```bash
./scripts/eshop.sh restore --debug --agent
```

`--debug` selects troubleshooting output even when `--agent` is present. The
`--agent` option still disables color in child utilities.

## Agent Coding Harness

The `.agents` and `.claude` directories, as well as `.mcp.json`, are intentionally not committed. This lets each developer choose their own agent coding tools and configuration without imposing them on others.

The `AGENTS.md` contains common instructions. You can add `AGENTS.local.md` for your own instructions and to override common agent instructions.
