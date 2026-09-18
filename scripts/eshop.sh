#!/usr/bin/env sh

# Bootstraps and runs the EShop.Cli developer tool (Linux). Ensures Docker is
# available, builds the utility image if needed, publishes EShop.Cli self-contained
# for this machine's architecture via that image (only when stale), then execs the
# published binary, forwarding every argument. Intentionally small and stable - all
# other developer workflows, orchestration, and business logic live in EShop.Cli
# itself.
#
# POSIX sh, not bash: this script only needs features every POSIX-compliant shell
# supports (dash, ash/busybox, ksh, bash-as-sh), so it runs on a minimal system with no
# bash installed. That rules out bash-only conveniences used elsewhere in this repo
# (`set -o pipefail`, `[[ ]]`, `${BASH_SOURCE[0]}`) - see the comments below for the
# POSIX equivalent used instead.
set -eu

# $0 is the POSIX-portable equivalent of bash's ${BASH_SOURCE[0]} for a script that is
# always executed directly (never sourced), which is the only way this one is invoked.
X_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "${X_ROOT}"

X_IMAGE_TAG=eshop-utility:local
X_DOCKER_SOCKET="${ESHOP_DOCKER:-/var/run/docker.sock}"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required. Install Docker and try again." >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker is installed but not running. Start Docker and try again." >&2
  exit 1
fi

X_ARCH="$(uname -m)"
case "${X_ARCH}" in
  x86_64) X_RID=linux-x64 ;;
  aarch64) X_RID=linux-arm64 ;;
  *)
    echo "Unsupported architecture: ${X_ARCH}" >&2
    exit 1
    ;;
esac

X_PUBLISH_DIR="tmp/cli-publish/${X_RID}"
X_BINARY="${X_PUBLISH_DIR}/EShop.Cli"

# Rebuild the utility image whenever its own content changes.
X_IMAGE_SOURCE_HASH="$(
  cat scripts/Containerfile global.json .nvmrc package.json Directory.Packages.props | sha256sum | cut -d ' ' -f1
)"
X_IMAGE_STORED_HASH="$(
  docker image inspect --format '{{ index .Config.Labels "eshop.source-hash" }}' "${X_IMAGE_TAG}" 2>/dev/null || true
)"
if [ "${X_IMAGE_STORED_HASH}" != "${X_IMAGE_SOURCE_HASH}" ]; then
  echo "Building utility image '${X_IMAGE_TAG}'..."
  docker build --label "eshop.source-hash=${X_IMAGE_SOURCE_HASH}" --progress=plain -t "${X_IMAGE_TAG}" -f scripts/Containerfile .
fi

X_SOURCE_HASH_FILE="${X_PUBLISH_DIR}/.source-hash"

# A content hash, not file mtimes, decides staleness.
X_CURRENT_HASH="$(
  find src/dev/EShop.Cli \( -name obj -o -name bin \) -prune -o \( -name '*.cs' -o -name '*.csproj' \) -type f -print \
    | LC_ALL=C sort \
    | xargs sha256sum \
    | sha256sum \
    | cut -d ' ' -f1
)"

X_NEEDS_PUBLISH=1
if [ -x "${X_BINARY}" ] && [ -f "${X_SOURCE_HASH_FILE}" ]; then
  X_PREVIOUS_HASH="$(cat "${X_SOURCE_HASH_FILE}")"
  if [ "${X_PREVIOUS_HASH}" = "${X_CURRENT_HASH}" ]; then
    X_NEEDS_PUBLISH=0
  fi
fi

if [ "${X_NEEDS_PUBLISH}" -eq 1 ]; then
  echo "Publishing EShop.Cli for ${X_RID}..."
  mkdir -p .cache/home
  # Rootless dockerd maps container uid/gid 0 to the invoking host user and ids
  # 1-65535 to a subordinate range (see /etc/subuid, /etc/subgid) - so a plain `stat`
  # of the socket from the host reports a group id that's meaningless inside the
  # container's own id space, and passing it to --group-add fails with "setgroups:
  # invalid argument". Root inside the container needs no such mapping: it's already
  # the invoking host user (via the same uid-0 mapping) and has DAC-override within its
  # own namespace, so it can read/write the bind-mounted socket regardless of its group.
  # No `set --`/array trick to share flags between the two branches below: this script
  # relies on the original "$@" later (`exec "${X_BINARY}" "$@"`), and POSIX sh has no
  # arrays other than the positional parameters - so the two docker run invocations are
  # duplicated instead of built up piecemeal.
  if docker info --format '{{.SecurityOptions}}' 2>/dev/null | grep -q rootless; then
    docker run --rm --network host --user 0:0 \
      -v "${X_ROOT}:/workspace" -w /workspace \
      -v "${X_DOCKER_SOCKET}:${X_DOCKER_SOCKET}" \
      --env-file scripts/container.env \
      "${X_IMAGE_TAG}" \
      dotnet publish src/dev/EShop.Cli/EShop.Cli.csproj -r "${X_RID}" --self-contained true -o "${X_PUBLISH_DIR}"
  else
    X_UID="$(id -u)"
    X_GID="$(id -g)"
    X_DOCKER_GID="$(stat -c '%g' "${X_DOCKER_SOCKET}")"
    docker run --rm --network host --user "${X_UID}:${X_GID}" --group-add "${X_DOCKER_GID}" \
      -v "${X_ROOT}:/workspace" -w /workspace \
      -v "${X_DOCKER_SOCKET}:${X_DOCKER_SOCKET}" \
      --env-file scripts/container.env \
      "${X_IMAGE_TAG}" \
      dotnet publish src/dev/EShop.Cli/EShop.Cli.csproj -r "${X_RID}" --self-contained true -o "${X_PUBLISH_DIR}"
  fi
  echo "${X_CURRENT_HASH}" > "${X_SOURCE_HASH_FILE}"
fi

exec "${X_BINARY}" "$@"
