#!/usr/bin/env bash
# auto-format-touched.sh
# Claude PostToolUse Edit|Write hook.
# Formats the touched file so the harness format gate passes:
#   *.java -> Spotless (Maven)      *.cs -> dotnet format
# Best effort — don't fail the agent's edit on a formatting error; surface it instead.

set -euo pipefail

HOOK_LIB="$(cd "$(dirname "${BASH_SOURCE[0]}")/lib" && pwd)"
# shellcheck source=lib/preflight.sh
. "$HOOK_LIB/preflight.sh"

require_jq

input="$(cat)"
file_path="$(echo "$input" | jq -r '.tool_input.file_path // .tool_input.path // empty')"
# Windows: Claude Code passes backslash paths; files_in_scope uses forward slashes.
file_path="${file_path//\\//}"
[ -z "$file_path" ] && exit 0

case "$file_path" in
  *.java) fmt=java ;;
  *.cs)   fmt=dotnet ;;
  *) exit 0 ;;
esac

if [ "$fmt" = "java" ]; then
  # Only run if Maven and Spotless are configured.
  if [ -f pom.xml ] && grep -q 'spotless-maven-plugin' pom.xml; then
    # Format-only on the single file. spotless:apply rewrites in place.
    mvn -q spotless:apply -DspotlessFiles="$file_path" >/dev/null 2>&1 || \
      echo "WARN: Spotless apply failed on $file_path (continuing)" >&2
  fi
else
  # Only run if this looks like a .NET workspace with style rules to apply.
  if ls ./*.sln ./*.slnx >/dev/null 2>&1 || [ -f global.json ]; then
    dotnet format --include "$file_path" --no-restore >/dev/null 2>&1 || \
      echo "WARN: dotnet format failed on $file_path (continuing)" >&2
  fi
fi

exit 0
