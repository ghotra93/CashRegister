#!/usr/bin/env bash
# preflight.sh
# Shared dependency check for the hooks. Sourced, not executed.
#
# Every hook parses its stdin payload with jq. When jq is missing the hook used
# to die with exit 127, which the harness reports as an error but which does NOT
# block the tool call -- so the TDD gate silently stopped enforcing anything.
# require_jq turns that silent failure into an explicit block.
#
# Hooks are spawned with the editor's PATH, which often omits per-user install
# locations (winget Links, Scoop, Homebrew). Rather than fail on a jq that IS
# installed but unlisted, probe the usual places first.

_add_to_path_if_has_jq() {
  [ -d "$1" ] || return 1
  [ -x "$1/jq" ] || [ -x "$1/jq.exe" ] || return 1
  case ":$PATH:" in
    *":$1:"*) ;;
    *) PATH="$PATH:$1"; export PATH ;;
  esac
  return 0
}

find_jq() {
  command -v jq >/dev/null 2>&1 && return 0

  local candidates=(
    "$HOME/AppData/Local/Microsoft/WinGet/Links"
    "$HOME/scoop/shims"
    "/c/ProgramData/chocolatey/bin"
    "/c/Program Files/Git/usr/bin"
    "/usr/local/bin"
     "/opt/homebrew/bin"
    "/usr/bin"
  )
  local d
  for d in "${candidates[@]}"; do
    _add_to_path_if_has_jq "$d" && return 0
  done

  # winget installs the binary under a versioned package directory.
  local pkgdir="$HOME/AppData/Local/Microsoft/WinGet/Packages"
  if [ -d "$pkgdir" ]; then
    while IFS= read -r d; do
      [ -n "$d" ] || continue
      _add_to_path_if_has_jq "$d" && return 0
    done < <(find "$pkgdir" -maxdepth 2 -type d -name '*jq*' 2>/dev/null)
  fi

  return 1
}

require_jq() {
  if ! find_jq; then
    echo "BLOCKED: 'jq' is not on PATH, so the TDD guard hooks cannot read their input." >&2
    echo "Install jq, then retry. Windows: winget install jqlang.jq   macOS: brew install jq   Debian/Ubuntu: sudo apt-get install jq" >&2
    echo "If jq IS installed, add its directory to PATH or to the candidates list in .claude/hooks/lib/preflight.sh." >&2
    echo "To work without the guards, remove the hooks block from .claude/settings.json -- this disables test-first enforcement." >&2
    exit 2
  fi
}
