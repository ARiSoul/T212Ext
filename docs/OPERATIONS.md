# Operations

Running T212Ext on the server once it is set up ([DEPLOYMENT.md](DEPLOYMENT.md)). The server, SQL Server, Caddy, the backup storage and the `sql` helper are traceon's: see traceon's `docs/OPERATIONS.md` for those.

## Contents

1. [Deploying an update](#1-deploying-an-update)
2. [Rolling back](#2-rolling-back)
3. [Status and logs](#3-status-and-logs)
4. [Users and sign-in](#4-users-and-sign-in)
5. [Trading 212 accounts](#5-trading-212-accounts)
6. [Changing secrets](#6-changing-secrets)
7. [Backups and restore](#7-backups-and-restore)
8. [Database](#8-database)

## Conventions

- Server: `ssh deploy@<ip>`, repository in `~/t212ext`, traceon in `~/traceon`.
- Settings and secrets live only in `~/t212ext/.env` (template: [.env.example](../.env.example)). Never paste their values into chats, issues or logs.
- The working tree has no local changes (`git status` shows nothing modified). Do not edit tracked files on the server.
- `sql "..."` is traceon's helper (traceon `docs/OPERATIONS.md`, Conventions); it runs as `sa`. For this app's database, start the query with `USE T212Ext;`.

## 1. Deploying an update

After a PR is merged to `main` (and its tag created):

1. Read the version's section in [UPGRADING.md](UPGRADING.md), if there is one.
2. Take a backup:

```bash
DB_NAME=T212Ext ~/traceon/ops/backup/traceon-backup.sh
```

3. Pull and rebuild:

```bash
cd ~/t212ext && git pull && docker compose up -d --build
```

   The keys volume stays; migrations apply when the app starts.

4. Check: `t212ext` is `healthy` after about a minute, `/health` answers, the version at the bottom of the menu is the new one.

```bash
docker compose ps
```

```bash
curl -s https://<T212EXT_DOMAIN>/health
```

**Changing only `.env`** needs no build: `docker compose up -d` recreates the container with the new values.

A sync cut short by the restart keeps what it saved (history jobs save page by page), and the sync on startup carries on.

## 2. Rolling back

- **Code:** go back to the previous tag and rebuild.

```bash
cd ~/t212ext && git checkout v<previous> && docker compose up -d --build
```

  Migrations are not reverted. Additive ones (new tables, new nullable columns) do not bother the previous version; otherwise restore the backup taken before the deploy (section 7). Return with `git checkout main && git pull`.
- **A setting:** put the old value back in `.env` and run `docker compose up -d`.

## 3. Status and logs

```bash
cd ~/t212ext && docker compose ps
```

```bash
docker compose logs --tail 100 t212ext
```

```bash
docker compose logs -f --since 10m t212ext
```

Logs rotate at 5 files of 10 MB. Worth knowing:

- `Failed sign-in for <user> from <address>`: a wrong password or code. After 5 attempts a minute from one address the sign-in page asks to wait.
- `API key refused; remaining jobs skipped`: Trading 212 answered 401, so the key was revoked or expired; see section 5.
- `Sync job <job> failed for account <id>`: the **Sync log** page has the detail. A 403 there means the key lacks a permission ([T212-API.md](T212-API.md)).

**Restart** (keeps everything):

```bash
cd ~/t212ext && docker compose restart t212ext
```

Memory and CPU next to traceon:

```bash
docker stats --no-stream
```

## 4. Users and sign-in

Users are in `.env` (`Auth__Users__N__*`, [DEPLOYMENT.md](DEPLOYMENT.md#5-sign-in)). After any change, `docker compose up -d`.

- **Change a password:** a new hash with `docker compose run --rm -it t212ext hash-password`, into `Auth__Users__0__PasswordHash`. Every session of that user ends.
- **New authenticator (phone lost or replaced):** if the secret is in the password manager, add it to the new phone. Otherwise create a new one with `docker compose run --rm t212ext new-totp-secret <username>`. Changing it also ends the sessions.
- **Sign out everywhere** without changing anything: delete the data protection keys. This also resets everyone's language choice.

```bash
cd ~/t212ext && docker compose down && docker volume rm t212ext_keys && docker compose up -d
```

- **Locked out** (no password, no code): set a new hash and remove the `TotpSecret` line in `.env`, `docker compose up -d`, sign in, then set up a new secret.

A session lasts 14 days from the last visit.

## 5. Trading 212 accounts

- **New account:** a new block in `.env` (`T212__Accounts__1__Key`, `__Name`, `__Environment`, `__ApiKey`, `__ApiSecret`), then `docker compose up -d`. The first sync of that account runs at once.
- **Replace a key** (revoked, leaked, expired): generate a new one in Trading 212 (restricted to the server's IP), put it in `.env`, `docker compose up -d`, then delete the old key in Trading 212.
- **Never change an account's `Key`:** the database knows the account by it. Its `Name` can change freely.

## 6. Changing secrets

| Secret | Where | How |
|---|---|---|
| Database password | `T212EXT_DB_PASSWORD` | Generate a new one (the `sed` line in [DEPLOYMENT.md](DEPLOYMENT.md#4-database)), run `~/t212ext/ops/db/create-database.sh`, then `docker compose up -d`. |
| Password, authenticator secret | `Auth__Users__N__*` | Section 4. |
| Trading 212 key and secret | `T212__Accounts__N__*` | Section 5. |
| Data protection keys | volume `t212ext_keys` | Section 4, "sign out everywhere". Never in `.env`. |

If a secret leaks, change it first and investigate afterwards.

## 7. Backups and restore

**Nightly**, by traceon's script with `DB_NAME=T212Ext` (cron line in [DEPLOYMENT.md](DEPLOYMENT.md#10-backups-and-monitoring)): encrypted, 7 daily and 4 weekly copies in `/var/backups/traceon` (files `T212Ext-*.bak.gz.gpg`), mirrored to the Storage Box.

```bash
tail -n 5 ~/t212ext-backup.log
```

**Restore test** (restores into a throwaway `T212Ext_RestoreTest`, compares row counts, drops it; the live database is only read):

```bash
DB_NAME=T212Ext ~/traceon/ops/backup/traceon-restore-test.sh
```

**Disaster restore** (replaces the live database). Stop the app, then follow traceon's "Disaster restore" with `T212Ext` in place of `TraceonDb`:

```bash
cd ~/t212ext && docker compose stop t212ext
```

```bash
gpg --batch --pinentry-mode loopback --passphrase-file ~/.config/traceon/backup.passphrase -d <T212Ext-file.bak.gz.gpg> | gunzip > /tmp/t212ext-restore.bak
```

```bash
docker cp /tmp/t212ext-restore.bak traceon-db:/var/opt/mssql/backup/t212ext-restore.bak && rm /tmp/t212ext-restore.bak
```

```bash
sql "ALTER DATABASE [T212Ext] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [T212Ext] FROM DISK = N'/var/opt/mssql/backup/t212ext-restore.bak' WITH REPLACE; ALTER DATABASE [T212Ext] SET MULTI_USER;"
```

On a new server, run `~/t212ext/ops/db/create-database.sh` now: it maps the restored database's user to the new login. Then:

```bash
docker exec traceon-db rm -f /var/opt/mssql/backup/t212ext-restore.bak && cd ~/t212ext && docker compose start t212ext
```

What a backup does not hold: `.env` (keep the secrets in the password manager) and the data protection keys (losing them only signs everyone out).

## 8. Database

Size, against SQL Server Express's 10 GB per database (years of history stay far below):

```bash
sql "SELECT SUM(size) * 8 / 1024 AS DataMB FROM sys.master_files WHERE database_id = DB_ID('T212Ext') AND type = 0;"
```

State (must be `ONLINE`, recovery `SIMPLE`):

```bash
sql "SELECT name, state_desc, recovery_model_desc FROM sys.databases WHERE name = 'T212Ext';"
```

Read-only queries from your PC with SSMS: the same SSH tunnel as traceon's (traceon `docs/OPERATIONS.md`, Database), database `T212Ext`. Prefer the `t212ext` login to `sa`.
