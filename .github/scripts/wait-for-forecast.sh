#!/usr/bin/env bash
# Wartet, solange in Prod ein Prognose-Lauf läuft (/health/run = running), damit der Deploy ihn nicht abbricht.
# Höchstens 45 Minuten (90 × 30 s). Danach geht der Deploy trotzdem weiter: Ein abgebrochener Lauf wird beim Start nachgeholt.
set -uo pipefail

: "${APP_URL:?APP_URL fehlt}"

for i in $(seq 1 90); do
  state=$(curl -sf --max-time 20 "$APP_URL/health/run" | head -c 20 || true)
  state=${state:-unknown}
  if [ "$state" != "running" ]; then
    echo "Prognose-Lauf: $state, Deploy geht weiter"
    exit 0
  fi
  echo "Prognose-Lauf läuft, warte ($i/90)"
  sleep 30
done

echo "::warning::Der Prognose-Lauf läuft nach 45 Minuten noch. Der Deploy geht weiter; der Lauf wird beim Start nachgeholt."
