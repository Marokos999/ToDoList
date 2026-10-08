#!/usr/bin/env bash
# Dumps the production database to backups/ (git-ignored). Usage:
#   DATABASE_URL='postgresql://user:password@host/db?sslmode=require' ./scripts/backup-db.sh
# Runs pg_dump inside a postgres container, so only Docker is needed.
set -euo pipefail

: "${DATABASE_URL:?Set DATABASE_URL to the Neon connection URL}"

mkdir -p backups
out="backups/prod-$(date +%Y-%m-%d-%H%M).sql"
docker run --rm postgres:17-alpine pg_dump "$DATABASE_URL" --no-owner --no-acl > "$out"
echo "Saved $out ($(wc -c < "$out") bytes)"
