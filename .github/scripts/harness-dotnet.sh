#!/usr/bin/env bash
# Runs every harness gate and writes artifacts/harness-summary.json.
#   ./.github/scripts/harness-dotnet.sh            # human-readable progress on stderr
#   ./.github/scripts/harness-dotnet.sh --report   # also prints the summary JSON on stdout
# The gate logic lives in lib/harness.mjs (Node 24, already required by web/).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
exec node .github/scripts/lib/harness.mjs "$@"
