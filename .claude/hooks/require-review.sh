#!/usr/bin/env bash

set -uo pipefail

command=$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input",{}).get("command",""))' 2>/dev/null || true)

if ! printf '%s' "$command" | grep -Eq '(^|[;&|]|&&|\|\|)[[:space:]]*(rtk[[:space:]]+)?git([[:space:]]+-C[[:space:]]+[^[:space:]]+)?[[:space:]]+push([[:space:]]|$)'; then
  exit 0
fi

marker="$(git rev-parse --git-common-dir 2>/dev/null)/tauchplaetze-review-ok"
head=$(git rev-parse HEAD 2>/dev/null || true)

if [[ -f "$marker" && "$(cat "$marker")" == "$head" ]]; then
  exit 0
fi

message="Kein bestandener /review für diesen Stand (${head:0:7}). Der Push läuft trotzdem – Claude weist Marc darauf hin und holt den Review nach."
python3 -c 'import json,sys; m=sys.argv[1]; print(json.dumps({"systemMessage": m, "hookSpecificOutput": {"hookEventName": "PreToolUse", "additionalContext": m}}))' "$message"
exit 0
