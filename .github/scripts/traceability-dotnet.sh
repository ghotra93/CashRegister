#!/usr/bin/env bash
# Regenerates .specs/<feature-id>/07a-traceability.md from [Trait("AC", ...)] on xUnit tests,
# "[AC-NNN]" prefixes on Vitest test names, and acs_covered in .tdd-state.json.
# Exits non-zero when any active AC has no tagged test.
#   ./.github/scripts/traceability-dotnet.sh <feature-id>
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
if [ "$#" -ne 1 ]; then
  echo "usage: $0 <feature-id>" >&2
  exit 2
fi
exec node .github/scripts/lib/traceability.mjs "$1"
