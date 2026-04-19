#!/usr/bin/env bash
set -euo pipefail

MIN_LINE=${1:-75}
MIN_BRANCH=${2:-60}

REPORT=$(find tests/SoundCloudDigger.Tests/TestResults -name 'coverage.cobertura.xml' -print -quit)
if [[ -z "${REPORT:-}" ]]; then
  echo "No coverage report found under tests/SoundCloudDigger.Tests/TestResults" >&2
  exit 2
fi

line=$(grep -oE 'line-rate="[0-9.]+"' "$REPORT" | head -n1 | sed -E 's/.*"([0-9.]+)".*/\1/')
branch=$(grep -oE 'branch-rate="[0-9.]+"' "$REPORT" | head -n1 | sed -E 's/.*"([0-9.]+)".*/\1/')

line_pct=$(awk "BEGIN{printf \"%.2f\", ${line}*100}")
branch_pct=$(awk "BEGIN{printf \"%.2f\", ${branch}*100}")

printf "Coverage: line=%s%% (min %s%%), branch=%s%% (min %s%%)\n" \
  "$line_pct" "$MIN_LINE" "$branch_pct" "$MIN_BRANCH"

fail=0
awk "BEGIN{exit !(${line_pct} < ${MIN_LINE})}" && { echo "FAIL: line coverage below minimum" >&2; fail=1; }
awk "BEGIN{exit !(${branch_pct} < ${MIN_BRANCH})}" && { echo "FAIL: branch coverage below minimum" >&2; fail=1; }
exit $fail
