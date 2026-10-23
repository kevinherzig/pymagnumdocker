#!/bin/sh
# Optional: rebuild the web UI (needs Node 18+; not required on the Pi since web/dist is committed)
set -e
if [ "$1" = "--web" ]; then (cd web && npm ci && npm run build); fi
docker compose build
