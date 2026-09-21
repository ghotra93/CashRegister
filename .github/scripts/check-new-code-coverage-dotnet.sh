#!/usr/bin/env bash
# Coverage of changed C# lines under src/ versus BASE_REF (default origin/main).
# Reads artifacts/coverage/cobertura.xml (written by harness-dotnet.sh) and writes
# artifacts/new-code-coverage.json. Fails below NEW_CODE_THRESHOLD (default 0.95).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
exec node .github/scripts/lib/new-code-coverage.mjs "$@"
