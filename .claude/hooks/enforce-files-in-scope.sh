#!/usr/bin/env bash
# enforce-files-in-scope.sh
# Claude PreToolUse Edit|Write hook.
# Blocks edits outside the active task's files_in_scope, EXCEPT:
#   - .specs/** and build/config files are always allowed (artifacts).
#   - test code is allowed for the test-engineer agents (red step).
#
# This is a defense-in-depth alongside block-impl-without-failing-test.sh which
# already covers production code. This script extends the same check to test
# files. Path classification is shared — see lib/classify-path.sh.

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

# Artifacts always pass; production code is the sibling hook's job; only test
# code is enforced here.
case "$(classify_path "$file_path")" in
  test) ;;
  *) exit 0 ;;
esac

state_file="$(ls -t .specs/*/.tdd-state.json 2>/dev/null | head -n 1 || true)"
[ -z "$state_file" ] && exit 0  # no active feature; let it through (e.g. test-plan author)

# A legacy array-shaped `tasks` cannot be queried by key; stay out of the way
# rather than blocking every test edit (the sibling hook reports the problem).
jq -e '.tasks | type == "object"' "$state_file" >/dev/null 2>&1 || exit 0

active_task="$(jq -r '.active_task // empty' "$state_file")"
[ -z "$active_task" ] && exit 0

# Each declared path is bound to $s before comparing: inside `endswith(.)` the
# `.` would rebind to $f, making the test always true.
in_scope="$(jq -r --arg t "$active_task" --arg f "$file_path" '
  .tasks[$t].files_in_scope // []
  | map(. as $s | select($f == $s or ($f | endswith($s))))
  | length
' "$state_file")"

if [ "$in_scope" = "0" ]; then
  echo "BLOCKED: $file_path is not in task $active_task files_in_scope. Edit only the declared test paths, or update 04-tasks.md and re-plan." >&2
  exit 2
fi

exit 0
