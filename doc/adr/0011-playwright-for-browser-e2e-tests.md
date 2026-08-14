# 0011 - Playwright as the browser end-to-end testing tool

## Status

Accepted

Note: the Consequences below reference `scripts/build.bash`/`scripts/test.bash`/
`scripts/test-e2e.bash`, since replaced by the `eshop.sh` bootstrap script and
`EShop.Cli` (ADR 0016; ADR 0017 narrows `EShop.Cli` itself to Linux only, for now).

## Context

Some bugs in the Seller Portal login flow show up only with a real browser. Two examples:
a `redirect_uri` host-forwarding bug, and a `__Host-` cookie rejected over plain HTTP.
The BFF's xUnit tests make direct HTTP calls. They perform no browser navigation. The
frontend's jsdom-based component tests simulate a DOM, but jsdom implements no real page
navigation and no real cookie jar. Neither existing test tier can exercise a failure mode
that depends on either of those two things.

## Decision

The team adopts Playwright, through the `Microsoft.Playwright` .NET package, as the tool
for end-to-end tests that need a real browser to exercise these failure modes. Aspire's
own testing support (the `Aspire.Hosting.Testing` package's
`DistributedApplicationTestingBuilder`) starts the real AppHost inside the same test
process, so a test can drive a real headless browser against a live, fully-wired instance
of the application. `test/EShop.AppHost.E2ETests` is the first test project built this
way.

## Consequences

- These tests need a real headless browser (Chromium) with its own OS-level dependencies
  present wherever they run. The team does not assume this on a developer's own machine.
  The shared `eshop-utility` image bundles Chromium instead (see
  `scripts/Containerfile`).
- These tests also start the real AppHost, so they need Docker, and they take far longer
  than the existing test tiers. The team keeps them in their own project
  (`test/EShop.AppHost.E2ETests`), outside `EShop.slnx`, run only through
  `scripts/test-e2e.bash` - never `scripts/build.bash`/`scripts/test.bash`.
- Running the real AppHost and a real browser together, inside a container, surfaced its
  own set of infrastructure problems (Docker-outside-of-Docker networking, certificate
  trust, OIDC redirect URI validation). See `doc/CHRONICLE.md` for how the team solved
  those.
