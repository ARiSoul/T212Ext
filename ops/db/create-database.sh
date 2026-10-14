#!/usr/bin/env bash
# Creates the T212Ext database and its "t212ext" login on the shared SQL Server (ops/db/create-database.sql).
# Run once before the first start, again after changing T212EXT_DB_PASSWORD in .env, and after a restore
# on a new server. The passwords are read from .env files and passed by environment, never on a command line.
# The SQL Server belongs to the server's shared infrastructure (today in traceon's compose project):
#   SA_ENV_FILE   file holding the sa password    (default ~/traceon/.env)
#   SA_ENV_KEY    its variable name               (default DB_SA_PASSWORD)
#   DB_CONTAINER  the SQL Server container        (default traceon-db)
# Usage: ops/db/create-database.sh   (see docs/DEPLOYMENT.md)
set -Eeuo pipefail

T212EXT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
SA_ENV_FILE="${SA_ENV_FILE:-$HOME/traceon/.env}"
SA_ENV_KEY="${SA_ENV_KEY:-DB_SA_PASSWORD}"
DB_CONTAINER="${DB_CONTAINER:-traceon-db}"

read_env() { grep -E "^$1=" "$2" | head -n1 | cut -d= -f2-; }

SQLCMDPASSWORD="$(read_env "$SA_ENV_KEY" "$SA_ENV_FILE")"
T212EXT_DB_PASSWORD="$(read_env T212EXT_DB_PASSWORD "$T212EXT_DIR/.env")"
: "${SQLCMDPASSWORD:?$SA_ENV_KEY not found in $SA_ENV_FILE}"
: "${T212EXT_DB_PASSWORD:?T212EXT_DB_PASSWORD not found in $T212EXT_DIR/.env}"

# The password goes inside a T-SQL string and the connection string: no quotes or semicolons.
case "$T212EXT_DB_PASSWORD" in
  *"'"* | *";"* | *'"'*) echo "T212EXT_DB_PASSWORD must not contain quotes or semicolons." >&2; exit 1 ;;
esac

export SQLCMDPASSWORD T212EXT_DB_PASSWORD
docker exec -i -e SQLCMDPASSWORD -e T212EXT_DB_PASSWORD "$DB_CONTAINER" \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b < "$T212EXT_DIR/ops/db/create-database.sql"
