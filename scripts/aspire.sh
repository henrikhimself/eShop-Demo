#!/usr/bin/env bash

# Wrapper for the `aspire` CLI that runs it against the same .cache/home sandbox
# EShop.Cli's ToolExecutor uses for local tools (see `eshop.sh env`), instead of the
# developer's real $HOME. Resolves `aspire`'s location on PATH first, before switching
# HOME below - aspire is typically installed under the developer's real $HOME (e.g.
# ~/.aspire/bin/aspire), so it must be looked up against the real environment.
#
# Bash, not POSIX sh: unlike eshop.sh, this script depends on eshop.sh's own
# `env --agent` output, which is only guaranteed sourceable in the project's stated
# developer shell (see AGENTS.md).
set -euo pipefail

if ! X_ASPIRE_BIN="$(command -v aspire)"; then
  echo "aspire CLI not found on PATH. Install it and try again." >&2
  exit 1
fi

X_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "${X_ROOT}"

# Captured into a variable, not `eval "$(...)"` directly - the latter would mask a
# failure in `eshop.sh env` itself (eval of an empty/partial string still succeeds).
X_ASPIRE_ENV="$(./scripts/eshop.sh env --agent)"
eval "${X_ASPIRE_ENV}"

X_SHOW_SCRIPTS=0
X_ASPIRE_ARGUMENTS=()
for X_ARGUMENT in "$@"; do
  if [ "${X_ARGUMENT}" = "--agent" ]; then
    X_SHOW_SCRIPTS=1
  else
    X_ASPIRE_ARGUMENTS+=("${X_ARGUMENT}")
  fi
done

if [ "${X_SHOW_SCRIPTS}" -eq 1 ]; then
  printf '%s\n\n' '# Scripts'
  printf '%s\n' "aspire.sh - Runs the Aspire CLI in the repository-local sandbox that EShop.Cli uses for local tools"
  printf '%s\n' "eshop.sh - Bootstraps and runs EShop.Cli, which provides the project development workflows"
  printf '\n'
fi

exec "${X_ASPIRE_BIN}" "${X_ASPIRE_ARGUMENTS[@]}"
