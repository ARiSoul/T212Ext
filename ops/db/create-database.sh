#!/usr/bin/env bash
# Creates the T212Ext database and its "t212ext" login on traceon's SQL Server (ops/db/create-database.sql).
# Run once before the first start, and again after changing T212EXT_DB_PASSWORD in .env.
# The passwords are read from the two .env files and passed by environment, never on a command line.
# Usage: ops/db/create-database.sh   (see docs/DEPLOYMENT.md)
set -Eeuo pipefail

T212EXT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
TRACEON_DIR="${TRACEON_DIR:-$HOME/traceon}"
CONTAINER="${DB_CONTAINER:-traceon-db}"

read_env() { grep -E "^$1=" "$2" | head -n1 | cut -d= -f2-; }

SQLCMDPASSWORD="$(read_env DB_SA_PASSWORD "$TRACEON_DIR/.env")"
T212EXT_DB_PASSWORD="$(read_env T212EXT_DB_PASSWORD "$T212EXT_DIR/.env")"
: "${SQLCMDPASSWORD:?DB_SA_PASSWORD not found in $TRACEON_DIR/.env}"
: "${T212EXT_DB_PASSWORD:?T212EXT_DB_PASSWORD not found in $T212EXT_DIR/.env}"

# The password goes inside a T-SQL string and the connection string: no quotes or semicolons.
case "$T212EXT_DB_PASSWORD" in
  *"'"* | *";"* | *'"'*) echo "T212EXT_DB_PASSWORD must not contain quotes or semicolons." >&2; exit 1 ;;
esac

export SQLCMDPASSWORD T212EXT_DB_PASSWORD
docker exec -i -e SQLCMDPASSWORD -e T212EXT_DB_PASSWORD "$CONTAINER" \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b < "$T212EXT_DIR/ops/db/create-database.sql"
