#!/usr/bin/env bash
# Nightly backup of the T212Ext database, kept locally with retention and mirrored off the machine.
#   - full BACKUP ... WITH CHECKSUM, signed in as the app's own login (not sa)
#   - gzip (tested) + gpg (AES256, mandatory), 7 daily + 4 weekly (Sunday) copies by default
#   - rsync --delete to a Hetzner Storage Box folder of its own, so the remote follows the same retention
# The full check (restore into a throwaway database) is t212ext-restore-test.sh, run weekly.
# Usage: ops/backup/t212ext-backup.sh   (cron: see docs/DEPLOYMENT.md, "Backups and monitoring")
set -Eeuo pipefail

T212EXT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
CONFIG="${BACKUP_CONFIG:-$T212EXT_DIR/ops/backup/backup.env}"
DB_NAME="T212Ext"

# shellcheck source=/dev/null
source "$CONFIG"
: "${REMOTE:?REMOTE not set in $CONFIG}" "${BACKUP_DIR:?}" "${SSH_KEY:?}" "${DB_CONTAINER:?}"
# Backups hold the whole portfolio and tax history: encryption is mandatory.
: "${GPG_PASSPHRASE_FILE:?GPG_PASSPHRASE_FILE not set in $CONFIG (backups must be encrypted)}"
[ -r "$GPG_PASSPHRASE_FILE" ] || { echo "Cannot read passphrase file $GPG_PASSPHRASE_FILE" >&2; exit 1; }
SSH_PORT="${SSH_PORT:-23}"; KEEP_DAILY="${KEEP_DAILY:-7}"; KEEP_WEEKLY="${KEEP_WEEKLY:-4}"

T212EXT_DB_PASSWORD="$(grep -E '^T212EXT_DB_PASSWORD=' "$T212EXT_DIR/.env" | head -n1 | cut -d= -f2-)"
: "${T212EXT_DB_PASSWORD:?T212EXT_DB_PASSWORD not found in $T212EXT_DIR/.env}"
export SQLCMDPASSWORD="$T212EXT_DB_PASSWORD"

ping() { if [ -n "${PING_URL:-}" ]; then curl -fsS -m 10 --retry 3 "$PING_URL$1" > /dev/null || true; fi; }
trap 'echo "$(date -Is) backup FAILED (line $LINENO)" >&2; ping /fail' ERR

# The password goes by environment (-e NAME without a value), never on a command line.
sqlcmd() {
  docker exec -e SQLCMDPASSWORD "$DB_CONTAINER" \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U t212ext -C -b "$@"
}

stamp="$(date -u +%Y%m%d-%H%M%S)"
name="$DB_NAME-$stamp.bak"
inner="/var/opt/mssql/backup/$name"

mkdir -p "$BACKUP_DIR/daily" "$BACKUP_DIR/weekly"
docker exec "$DB_CONTAINER" mkdir -p /var/opt/mssql/backup

echo "$(date -Is) backing up $DB_NAME"
sqlcmd -Q "BACKUP DATABASE [$DB_NAME] TO DISK = N'$inner' WITH INIT, CHECKSUM;"

docker cp "$DB_CONTAINER:$inner" "$BACKUP_DIR/daily/$name"
docker exec "$DB_CONTAINER" rm -f "$inner"

file="$BACKUP_DIR/daily/$name"
gzip -9 "$file"; file="$file.gz"
gzip -t "$file"
gpg --batch --yes --pinentry-mode loopback --passphrase-file "$GPG_PASSPHRASE_FILE" \
    --symmetric --cipher-algo AES256 -o "$file.gpg" "$file"
rm -f "$file"; file="$file.gpg"
chmod 600 "$file"

# Sunday copy goes to weekly/
if [ "$(date -u +%u)" = "7" ]; then
  cp -p "$file" "$BACKUP_DIR/weekly/"
fi

prune() { # keep the newest $2 files in $1
  find "$1" -maxdepth 1 -type f -name "$DB_NAME-*" -printf '%T@ %p\n' | sort -rn \
    | tail -n +"$(( $2 + 1 ))" | cut -d' ' -f2- | xargs -r rm -f
}
prune "$BACKUP_DIR/daily" "$KEEP_DAILY"
prune "$BACKUP_DIR/weekly" "$KEEP_WEEKLY"

echo "$(date -Is) uploading to $REMOTE"
rsync -a --delete -e "ssh -p $SSH_PORT -i $SSH_KEY -o BatchMode=yes" "$BACKUP_DIR/" "$REMOTE/"

echo "$(date -Is) done: $(basename "$file") ($(du -h "$file" | cut -f1))"
ping ""
