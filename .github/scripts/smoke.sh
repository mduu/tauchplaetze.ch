#!/usr/bin/env bash
# Rauchtest nach jedem Deploy (infrastructure.md, «Rauchtest»): prüft Inhalte, nicht nur HTTP 200.
# Aufruf: APP_URL=https://tauchplaetze.ch .github/scripts/smoke.sh
set -uo pipefail

: "${APP_URL:?APP_URL fehlt}"
failures=0

fetch() {
  local path="$1" attempt
  for attempt in 1 2 3; do
    body=$(curl -s --max-time 60 -w '\n%{http_code}' "$APP_URL$path" || true)
    code="${body##*$'\n'}"
    body="${body%$'\n'*}"
    [ "$code" = "200" ] && return 0
    sleep 10
  done
  return 1
}

fail() {
  echo "::error::Rauchtest: $1"
  failures=$((failures + 1))
}

for path in /robots.txt /sitemap.xml /llms.txt; do
  if fetch "$path"; then echo "ok   $path"; else fail "$path antwortet HTTP $code"; fi
done

if fetch /de/ueber; then
  if [[ "$body" != *"<address"* ]] || [[ "$body" == *"data-operator-missing"* ]]; then
    fail "/de/ueber zeigt keine Anschrift im Impressum"
  else
    echo "ok   /de/ueber (Impressum mit Anschrift)"
  fi
else
  fail "/de/ueber antwortet HTTP $code"
fi

if fetch /sitemap-de.xml; then
  site=$(grep -o '<loc>[^<]*/de/tauchplaetze/[^<]*</loc>' <<<"$body" | head -1 | sed -E 's#<loc>https?://[^/]+##; s#</loc>##')
  if [ -z "$site" ]; then
    fail "/sitemap-de.xml enthält keine Platzseite"
  elif fetch "$site"; then
    missing=""
    [[ "$body" == *"<title>"* ]] || missing="$missing title"
    [[ "$body" == *'rel="canonical"'* ]] || missing="$missing canonical"
    [[ "$body" == *"application/ld+json"* ]] || missing="$missing json-ld"
    if [ -n "$missing" ]; then fail "$site ohne:$missing"; else echo "ok   $site (Titel, Canonical, JSON-LD)"; fi
  else
    fail "$site antwortet HTTP $code"
  fi
else
  fail "/sitemap-de.xml antwortet HTTP $code"
fi

if [ "$failures" -gt 0 ]; then
  echo "Rauchtest: $failures Fehler"
  exit 1
fi
echo "Rauchtest bestanden"
