#!/usr/bin/env bash
set -euo pipefail

# ytpt installer — https://github.com/7KiLL/YTPlaylistTracker
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/7KiLL/YTPlaylistTracker/main/scripts/install.sh | bash
#
# Environment variables:
#   YTPT_VERSION      Install a specific version (e.g. v0.2.0). Default: latest.
#   YTPT_INSTALL_DIR  Override the default install directory.

REPO="7KiLL/YTPlaylistTracker"
GITHUB_API="https://api.github.com/repos/${REPO}/releases"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

say() {
    printf '%s\n' "$*"
}

err() {
    say "error: $*" >&2
    exit 1
}

need_cmd() {
    if ! command -v "$1" &>/dev/null; then
        err "need '$1' (command not found)"
    fi
}

# ---------------------------------------------------------------------------
# Detect platform
# ---------------------------------------------------------------------------

detect_os() {
    local os
    os="$(uname -s)"
    case "$os" in
        Linux)  echo "linux" ;;
        Darwin) echo "osx" ;;
        *)      err "unsupported operating system: $os (only Linux and macOS are supported)" ;;
    esac
}

detect_arch() {
    local arch
    arch="$(uname -m)"
    case "$arch" in
        x86_64|amd64)       echo "x64" ;;
        aarch64|arm64)      echo "arm64" ;;
        *)                  err "unsupported architecture: $arch (only x86_64 and arm64 are supported)" ;;
    esac
}

default_install_dir() {
    local os="$1"
    case "$os" in
        linux) echo "${HOME}/.local/bin" ;;
        osx)   echo "/usr/local/bin" ;;
    esac
}

# ---------------------------------------------------------------------------
# Resolve version
# ---------------------------------------------------------------------------

resolve_version() {
    if [[ -n "${YTPT_VERSION:-}" ]]; then
        echo "${YTPT_VERSION}"
        return
    fi

    say "Fetching latest release..."
    local tag
    tag="$(curl -fsSL "${GITHUB_API}/latest" | grep '"tag_name"' | sed -E 's/.*"tag_name":\s*"([^"]+)".*/\1/')"
    if [[ -z "$tag" ]]; then
        err "failed to determine latest release (GitHub API returned no tag_name)"
    fi
    echo "$tag"
}

# ---------------------------------------------------------------------------
# Download & install
# ---------------------------------------------------------------------------

download_and_install() {
    local version="$1"
    local rid="$2"
    local install_dir="$3"

    local asset="ytpt-${rid}.tar.gz"
    local url="https://github.com/${REPO}/releases/download/${version}/${asset}"

    local tmp
    tmp="$(mktemp -d)"
    # shellcheck disable=SC2064
    trap "rm -rf '$tmp'" EXIT

    say "Downloading ${asset} (${version})..."
    local http_code
    http_code="$(curl -fsSL -w '%{http_code}' -o "${tmp}/${asset}" "$url" 2>/dev/null)" || true
    if [[ ! -f "${tmp}/${asset}" ]] || [[ "$(wc -c < "${tmp}/${asset}")" -lt 100 ]]; then
        err "download failed — could not fetch ${url} (is the version '${version}' correct?)"
    fi

    say "Extracting..."
    tar -xzf "${tmp}/${asset}" -C "$tmp"

    if [[ ! -f "${tmp}/ytpt" ]]; then
        err "archive does not contain a 'ytpt' binary"
    fi

    mkdir -p "$install_dir"
    mv -f "${tmp}/ytpt" "${install_dir}/ytpt"
    chmod +x "${install_dir}/ytpt"

    say "Installed ytpt to ${install_dir}/ytpt"
}

# ---------------------------------------------------------------------------
# PATH check
# ---------------------------------------------------------------------------

check_path() {
    local install_dir="$1"

    case ":${PATH}:" in
        *":${install_dir}:"*) ;;
        *)
            say ""
            say "WARNING: '${install_dir}' is not in your PATH."
            say ""
            say "Add it by appending one of the following to your shell profile:"
            say ""
            say "  # bash (~/.bashrc or ~/.bash_profile)"
            say "  export PATH=\"${install_dir}:\$PATH\""
            say ""
            say "  # zsh (~/.zshrc)"
            say "  export PATH=\"${install_dir}:\$PATH\""
            say ""
            say "  # fish (~/.config/fish/config.fish)"
            say "  fish_add_path ${install_dir}"
            say ""
            say "Then restart your shell or run:  source ~/.bashrc"
            ;;
    esac
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

main() {
    need_cmd curl
    need_cmd tar
    need_cmd uname
    need_cmd mktemp
    need_cmd grep
    need_cmd sed

    local os arch rid install_dir version

    os="$(detect_os)"
    arch="$(detect_arch)"
    rid="${os}-${arch}"

    install_dir="${YTPT_INSTALL_DIR:-$(default_install_dir "$os")}"
    version="$(resolve_version)"

    say ""
    say "  ytpt installer"
    say "  =============="
    say "  OS:          ${os}"
    say "  Arch:        ${arch}"
    say "  RID:         ${rid}"
    say "  Version:     ${version}"
    say "  Install dir: ${install_dir}"
    say ""

    download_and_install "$version" "$rid" "$install_dir"

    check_path "$install_dir"

    say ""
    say "ytpt ${version} installed successfully!"
    say "Run 'ytpt --help' to get started."
    say ""
}

main "$@"
