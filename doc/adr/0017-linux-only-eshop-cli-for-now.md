# 0017 - `EShop.Cli` supports Linux only, for now

## Status

Accepted

Narrows [ADR 0016](./0016-eshop-cli-replaces-bash-scripts.md)'s statement that three
small per-OS scripts build and start `EShop.Cli`, to Linux only for now.

## Context

`EShop.Cli` shipped with three bootstrap scripts: `scripts/eshop.sh` (Linux),
`scripts/eshop.zsh` (macOS), and `scripts/eshop.ps1` (Windows). The tool's own
internals do not actually give the macOS and Windows scripts full parity with the
Linux one. For example, the container-fallback AppHost session and the dev-certificate
import into the utility container's NSS database both build a Bash/`sh` script whose
content assumes a Linux-flavored toolchain (`stat -c`, `certutil`, `pk12util`), and
`EShop.Cli`'s own automated test suite already assumes a Linux host. Carrying three
bootstrap scripts and a "supports three operating systems" claim while only Linux is
actually exercised and maintained is misleading to a new contributor.

## Decision

`EShop.Cli` supports Linux only, for now. The team removed `scripts/eshop.zsh` and
`scripts/eshop.ps1`, and any source code whose sole purpose was supporting them (for
example, the Windows-specific `PATHEXT` executable-extension lookup in
`LocalToolLocator`). `scripts/eshop.sh` is the one remaining bootstrap script.

## Consequences

- A contributor needs a Linux host (or a Linux VM/WSL2 environment) to build, lint,
  test, or run this solution.
- Re-adding Windows and/or macOS support is a deliberate future task, tracked in
  `doc/TODO.md`, not an accidental gap to silently work around.
- `README.md` and `AGENTS.md` document only the Linux/`eshop.sh` path.
