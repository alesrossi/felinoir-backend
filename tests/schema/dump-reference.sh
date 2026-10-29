#!/usr/bin/env bash
# Regenerates tests/schema/production.sql, the schema reference the drift test compares
# against (rework plan, step 0/1). Schema only, read-only; no data leaves the database.
#
# Usage: tests/schema/dump-reference.sh [container] [database] [user] [port]
# Defaults match the local docker-compose `db` service.
set -euo pipefail

container="${1:-felinoir-backend-db-1}"
database="${2:-cinema}"
user="${3:-cinema}"
port="${4:-5438}"

# --restrict-key pins the otherwise random \restrict token, so the output is stable.
docker exec "$container" pg_dump -U "$user" -p "$port" -d "$database" \
  --schema-only --no-owner --no-privileges --no-comments --restrict-key=felinoir \
  > "$(dirname "$0")/production.sql"
