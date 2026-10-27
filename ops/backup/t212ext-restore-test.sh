#!/usr/bin/env bash
# Restores a T212Ext backup into a throwaway database (t212ext_restore_test) on the app's own PostgreSQL,
# compares the row count of every table with the live database, then drops it. The live database is only
# read. Weekly from cron, and by hand with a copy fetched from the Storage Box.
# Usage: ops/backup/t212ext-restore-test.sh [backup-file]   (default: newest file in daily/)
set -Eeuo pipefail

T212EXT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
CONFIG="${BACKUP_CONFIG:-$T212EXT_DIR/ops/backup/backup.env}"
DB_NAME="t212ext"
TEST_DB="${DB_NAME}_restore_test"

# shellcheck source=/dev/null
source "$CONFIG"
: "${BACKUP_DIR:?}" "${GPG_PASSPHRASE_FILE:?GPG_PASSPHRASE_FILE not set in $CONFIG}"
DB_CONTAINER="${DB_CONTAINER:-t212ext-db}"

# Over the container's local socket, which the image trusts; the user owns both databases.
psql() { docker exec -i -e PGOPTIONS='-c client_min_messages=warning' "$DB_CONTAINER" psql -U t212ext -v ON_ERROR_STOP=1 -X -q -tA "$@"; }

newest() { find "$BACKUP_DIR/daily" -maxdepth 1 -type f -name "$DB_NAME-*" -printf '%T@ %p\n' | sort -rn | head -n1 | cut -d' ' -f2-; }
src="${1:-$(newest)}"
if [ -z "$src" ] || [ ! -r "$src" ]; then
  echo "No backup to restore (give a file, or run t212ext-backup.sh first)." >&2
  exit 1
fi
echo "Restoring from: $src"

work="$(mktemp -d)"
trap 'rm -rf "$work"; psql -d postgres -c "DROP DATABASE IF EXISTS $TEST_DB WITH (FORCE);" > /dev/null 2>&1 || true' EXIT

dump="$work/restore-test.dump"
case "$src" in
  *.gpg) gpg --batch --pinentry-mode loopback --passphrase-file "$GPG_PASSPHRASE_FILE" -d "$src" > "$dump" ;;
  *)     cp "$src" "$dump" ;;
esac

psql -d postgres -c "DROP DATABASE IF EXISTS $TEST_DB WITH (FORCE);" > /dev/null
psql -d postgres -c "CREATE DATABASE $TEST_DB;" > /dev/null
docker exec -i "$DB_CONTAINER" pg_restore -U t212ext -d "$TEST_DB" --no-owner --exit-on-error < "$dump"

counts() { # "table|rows" for every table of database $1, exact counts
  local tables table
  tables="$(psql -d "$1" -c "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY 1;")"
  for table in $tables; do
    echo "$table|$(psql -d "$1" -c "SELECT count(*) FROM \"$table\";")"
  done
}

echo
printf '%-45s %12s %12s\n' "table" "restored" "live now"
LC_ALL=C join -t'|' -a1 <(counts "$TEST_DB" | LC_ALL=C sort) <(counts "$DB_NAME" | LC_ALL=C sort) \
  | awk -F'|' '{printf "%-45s %12s %12s\n", $1, $2, $3}'

psql -d postgres -c "DROP DATABASE $TEST_DB WITH (FORCE);" > /dev/null
echo
echo "Restore OK. '$TEST_DB' dropped. Live counts may be higher: they include changes made after the backup."
