# 0016 - EShop.Cli replaces the Bash developer scripts

## Status

Accepted

[ADR 0017](./0017-linux-only-eshop-cli-for-now.md) narrows this ADR's "three small
per-OS scripts" statement to Linux only, for now.

## Context

The `scripts/` directory grew into a large set of Bash scripts. The scripts cover
setup, restore, build, format, test, and run tasks. Bash has no good way to write
automated tests for this amount of logic. Bash is hard to maintain at this size. Bash
support differs across operating systems: macOS ships an old Bash version, and many
Windows developers do not use Bash at all.

The scripts also call several Linux-only tools, for example `jq`, `find`, and `stat`.
A .NET library can replace many of these calls.

An AI coding agent runs these same developer commands. An agent needs stable, low-noise
output. A human developer wants a rich, colorful terminal experience instead.

## Decision

The team replaces the Bash scripts with a new .NET command-line tool, `EShop.Cli`,
built with the Spectre.Console.Cli library, with a full automated test suite.

The tool supports two output modes, one for a human developer and one for an AI coding
agent, and can run each external tool (for example `dotnet`, `pnpm`, or `rumdl`)
locally or inside the shared utility Docker container. Three small per-OS scripts
build and start the tool. See `doc/CHRONICLE.md` for the full design.

## Consequences

- `EShop.Cli` (`src/EShop.Cli/`) and its test project (`test/EShop.Cli.Tests/`)
  replace the developer-facing role of `scripts/*.bash` and `scripts/internal/`. See
  `AGENTS.md` "Common Commands" for the command reference, and `doc/CHRONICLE.md` for
  the full design.
- `AGENTS.md` instructs an AI coding agent to add `--agent` to every `eshop` command.
- A developer with `rumdl`, `shellcheck`, or `plantuml` installed locally can now use
  that local copy under `--tools auto` or `--tools local`. The old scripts always ran
  these three tools inside the container.
- `eshop test e2e` is the one exception to that local/container choice: it always
  runs inside the container, regardless of `--tools`, because it needs the
  container's Chromium install (see ADR 0011).
- `EShop.Cli` needs a Linux, macOS, or Windows self-contained .NET runtime download the
  first time a developer runs `scripts/eshop.sh`/`.zsh`/`.ps1`, or after a change to
  `EShop.Cli`'s own source code. The script re-uses the same published copy on every
  later run, until the source changes again.
