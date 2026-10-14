#!/usr/bin/env bash
# Restores a T212Ext backup into a throwaway database (T212Ext_RestoreTest) on the shared SQL Server,
# compares row counts per table with the live database, then drops it. The live database is only read.
# Restoring needs CREATE DATABASE, so this runs as sa (password read from SA_ENV_FILE, passed by
# environment). Weekly from cron, and by hand with a copy fetched from the Storage Box.
# Usage: ops/backup/t212ext-restore-test.sh [backup-file]   (default: newest file in daily/)
set -Eeuo pipefail

T212EXT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
CONFIG="${BACKUP_CONFIG:-$T212EXT_DIR/ops/backup/backup.env}"
DB_NAME="T212Ext"
TEST_DB="${DB_NAME}_RestoreTest"

# shellcheck source=/dev/null
source "$CONFIG"
: "${BACKUP_DIR:?}" "${DB_CONTAINER:?}" "${SA_ENV_FILE:?SA_ENV_FILE not set in $CONFIG}"
SQLCMDPASSWORD="$(grep -E "^${SA_ENV_KEY:-DB_SA_PASSWORD}=" "$SA_ENV_FILE" | head -n1 | cut -d= -f2-)"
: "${SQLCMDPASSWORD:?${SA_ENV_KEY:-DB_SA_PASSWORD} not found in $SA_ENV_FILE}"
export SQLCMDPASSWORD

sqlcmd() {
  docker exec -e SQLCMDPASSWORD "$DB_CONTAINER" \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -h -1 -W -s '|' "$@"
}

newest() { find "$BACKUP_DIR/daily" -maxdepth 1 -type f -name "$DB_NAME-*" -printf '%T@ %p\n' | sort -rn | head -n1 | cut -d' ' -f2-; }
src="${1:-$(newest)}"
[ -n "$src" ] && [ -r "$src" ] || { echo "No backup to restore (give a file, or run t212ext-backup.sh first)." >&2; exit 1; }
echo "Restoring from: $src"

work="$(mktemp -d)"
trap 'rm -rf "$work"; docker exec "$DB_CONTAINER" rm -f /var/opt/mssql/backup/t212ext-restore-test.bak || true' EXIT

bak="$work/restore-test.bak"
case "$src" in
  *.gpg) gpg --batch --pinentry-mode loopback --passphrase-file "$GPG_PASSPHRASE_FILE" -d "$src" | gunzip > "$bak" ;;
  *.gz)  gunzip -c "$src" > "$bak" ;;
  *)     cp "$src" "$bak" ;;
esac

docker exec "$DB_CONTAINER" mkdir -p /var/opt/mssql/backup
docker cp "$bak" "$DB_CONTAINER:/var/opt/mssql/backup/t212ext-restore-test.bak"
docker exec -u 0 "$DB_CONTAINER" chown mssql /var/opt/mssql/backup/t212ext-restore-test.bak 2>/dev/null || true

# Logical file names (data = D, log = L) from the backup itself.
files="$(sqlcmd -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'/var/opt/mssql/backup/t212ext-restore-test.bak';")"
data_name="$(echo "$files" | awk -F'|' '$3 == "D" {print $1; exit}')"
log_name="$(echo "$files" | awk -F'|' '$3 == "L" {print $1; exit}')"

sqlcmd -Q "RESTORE DATABASE [$TEST_DB] FROM DISK = N'/var/opt/mssql/backup/t212ext-restore-test.bak'
           WITH MOVE N'$data_name' TO N'/var/opt/mssql/data/$TEST_DB.mdf',
                MOVE N'$log_name'  TO N'/var/opt/mssql/data/${TEST_DB}_log.ldf',
                REPLACE, CHECKSUM;" > /dev/null

counts() {
  sqlcmd -d "$1" -Q "SET NOCOUNT ON;
    SELECT s.name + '.' + t.name, SUM(p.rows)
    FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
    GROUP BY s.name, t.name ORDER BY 1;"
}

echo
printf '%-45s %12s %12s\n' "table" "restored" "live now"
LC_ALL=C join -t'|' -a1 <(counts "$TEST_DB" | LC_ALL=C sort) <(counts "$DB_NAME" | LC_ALL=C sort) \
  | awk -F'|' '{printf "%-45s %12s %12s\n", $1, $2, $3}'

sqlcmd -Q "ALTER DATABASE [$TEST_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TEST_DB];" > /dev/null
echo
echo "Restore OK. '$TEST_DB' dropped. Live counts may be higher: they include changes made after the backup."
