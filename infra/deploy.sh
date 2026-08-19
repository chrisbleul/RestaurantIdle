#!/usr/bin/env bash
# Laeuft auf der EC2, in infra/. Zieht das zuletzt in der CI gebaute Bild von
# GHCR und rollt zurueck, wenn der Healthcheck danach fehlschlaegt. Analog
# ToDoCGO/infra/deploy.sh, aber nur ein "api"-Service (kein "web" -- die
# Unity-Engine laeuft nie hier, siehe docker-compose.yml).
#
# Aufruf:  ./deploy.sh [git-revision]
#   ohne Argument: neuestes Bild ("latest", also der letzte main-Push)
#   mit Argument:  ein konkreter Commit-SHA-Tag, z. B. fuer ein Rollback:
#                  ./deploy.sh 5575c03
set -euo pipefail
cd "$(dirname "$0")"

if [ ! -f .env ]; then
  echo "infra/.env fehlt (siehe .env.example, chmod 600, niemals committen)." >&2
  exit 1
fi

NEW_REVISION="${1:-latest}"
PREVIOUS_REVISION="$(docker compose --env-file .env images api --format json 2>/dev/null \
  | grep -o '"Tag":"[^"]*"' | head -1 | cut -d'"' -f4 || true)"

echo "Deploy: $NEW_REVISION (zuvor lief: ${PREVIOUS_REVISION:-unbekannt})"

GIT_REVISION="$NEW_REVISION" docker compose --env-file .env pull
GIT_REVISION="$NEW_REVISION" docker compose --env-file .env up -d

echo "Warte auf Healthcheck..."
ok=0
for i in $(seq 1 15); do
  if curl -sf "http://127.0.0.1:8098/api/health" >/dev/null; then
    ok=1
    break
  fi
  sleep 2
done

if [ "$ok" != "1" ]; then
  echo "Healthcheck fehlgeschlagen." >&2
  if [ -n "${PREVIOUS_REVISION:-}" ] && [ "$PREVIOUS_REVISION" != "$NEW_REVISION" ]; then
    echo "Rollback auf $PREVIOUS_REVISION..." >&2
    GIT_REVISION="$PREVIOUS_REVISION" docker compose --env-file .env up -d
  fi
  exit 1
fi

echo "OK — $NEW_REVISION läuft."
