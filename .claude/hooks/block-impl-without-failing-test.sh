#!/usr/bin/env bash
# block-impl-without-failing-test.sh
# Claude Code PreToolUse hook for Edit|Write.
# Refuses to edit production code unless .specs/<active>/.tdd-state.json shows
# phase=red with a non-empty red_failure_excerpt and the file is in files_in_scope.
#
# "Production code" is whatever lib/classify-path.sh calls `prod`: src/main/** on
# the JVM stack, src/**/*.cs on the .NET stack.
#
# Input: JSON on stdin (Claude Code hook protocol). We read tool_input.file_path.
# Output: exit 0 to allow, exit 2 + stderr message to block.

set -euo pipefail

HOOK_LIB="$(cd "$(dirname "${BASH_SOURCE[0]}")/lib" && pwd)"
# shellcheck source=lib/preflight.sh
. "$HOOK_LIB/preflight.sh"
# shellcheck source=lib/classify-path.sh
. "$HOOK_LIB/classify-path.sh"

require_jq

input="$(cat)"

file_path="$(echo "$input" | jq -r '.tool_input.file_path // .tool_input.path // empty')"
# Windows: Claude Code passes backslash paths; files_in_scope uses forward slashes.
file_path="${file_path//\\//}"
[ -z "$file_path" ] && exit 0

# Only enforce on production code (JVM src/main/**, .NET src/**/*.cs).
[ "$(classify_path "$file_path")" = "prod" ] || exit 0

# Find the active feature: the most recent .specs/<id>/.tdd-state.json by mtime.
state_file="$(ls -t .specs/*/.tdd-state.json 2>/dev/null | head -n 1 || true)"
if [ -z "$state_file" ] || [ ! -f "$state_file" ]; then
  echo "BLOCKED: no .specs/<feature>/.tdd-state.json found. Run /build <task-id> so the test-engineer writes the failing test first." >&2
  exit 2
fi

# The canonical shape has `tasks` as a map keyed by task id. A legacy array
# shape makes every .tasks[$t] lookup abort under `set -e`, so report it plainly.
if ! jq -e '.tasks | type == "object"' "$state_file" >/dev/null 2>&1; then
  echo "BLOCKED: $state_file has a legacy array-shaped \"tasks\" field. Re-run /plan (or /net-plan) to migrate it to the keyed-map shape: {\"tasks\": {\"T-001\": {...}}}." >&2
  exit 2
fi

active_task="$(jq -r '.active_task // empty' "$state_file")"
if [ -z "$active_task" ]; then
  echo "BLOCKED: no active_task in $state_file. Run /build <task-id>." >&2
  exit 2
fi

phase="$(jq -r --arg t "$active_task" '.tasks[$t].phase // empty' "$state_file" 2>/dev/null || echo '')"
red_excerpt="$(jq -r --arg t "$active_task" '.tasks[$t].red_failure_excerpt // empty' "$state_file" 2>/dev/null || echo '')"

if [ "$phase" != "red" ] && [ "$phase" != "green" ] && [ "$phase" != "refactor" ] && [ "$phase" != "simplify" ]; then
  echo "BLOCKED: task $active_task phase is '$phase'. Cannot edit src/main/** without a failing test (phase=red)." >&2
  exit 2
fi

if [ "$phase" = "red" ] && [ -z "$red_excerpt" ]; then
  echo "BLOCKED: task $active_task phase=red but red_failure_excerpt is empty. The failing test was not actually run, or it passed." >&2
  exit 2
fi

# Files in scope check. Each declared path is bound to $s before comparing:
# inside `endswith(.)` the `.` would rebind to $f, making the test always true.
in_scope="$(jq -r --arg t "$active_task" --arg f "$file_path" '
  .tasks[$t].files_in_scope // []
  | map(. as $s | select($f == $s or ($f | endswith($s))))
  | length
' "$state_file")"

if [ "$in_scope" = "0" ]; then
  echo "BLOCKED: $file_path is not in task $active_task files_in_scope. Edit only the declared paths, or update the task entry first (and re-run /plan)." >&2
  exit 2
fi

exit 0
