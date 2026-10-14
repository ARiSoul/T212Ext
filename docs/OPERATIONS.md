# Operations

Running T212Ext on the server once it is set up ([DEPLOYMENT.md](DEPLOYMENT.md)). The shared infrastructure (Caddy, SQL Server, the networks `edge` and `data`) lives in traceon's compose project: its `docs/OPERATIONS.md` covers it, and its section "Other apps on this server" says what it keeps stable for T212Ext.

## Contents

1. [Deploying an update](#1-deploying-an-update)
2. [Rolling back](#2-rolling-back)
3. [Status and logs](#3-status-and-logs)
4. [Users and sign-in](#4-users-and-sign-in)
5. [Trading 212 accounts](#5-trading-212-accounts)
6. [Changing secrets](#6-changing-secrets)
7. [Backups and restore](#7-backups-and-restore)
8. [Database](#8-database)
9. [Notifications](#9-notifications)

## Conventions

- Server: `ssh deploy@<ip>`, repository in `~/t212ext`. The shared infrastructure is in `~/traceon`.
- Settings and secrets live only in `~/t212ext/.env` (template: [.env.example](../.env.example)). Never paste their values into chats, issues or logs.
- The working tree has no local changes (`git status` shows nothing modified). Do not edit tracked files on the server.
- `t212sql "..."` runs a query in the `T212Ext` database as the app's own login (it cannot touch anything else). It is a shell function that reads the password from `.env` without printing it; define it in each SSH session, or append it once to `~/.bashrc`:

```bash
t212sql() { docker exec -e SQLCMDPASSWORD="$(grep -E '^T212EXT_DB_PASSWORD=' ~/t212ext/.env | cut -d= -f2-)" traceon-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U t212ext -d T212Ext -C -b -Q "$1"; }
```

- `sql "..."` is the infrastructure's `sa` helper (traceon `docs/OPERATIONS.md`, Conventions). Only the restore below needs it.

## 1. Deploying an update

After a PR is merged to `main` (and its tag created):

1. Read the version's section in [UPGRADING.md](UPGRADING.md), if there is one.
2. Take a backup:

```bash
~/t212ext/ops/backup/t212ext-backup.sh
```

3. Pull and rebuild:

```bash
cd ~/t212ext && git pull && docker compose up -d --build
```

   The keys volume stays; migrations apply when the app starts.

4. Check: `t212ext-web` is `healthy` after about a minute, `/health` answers, the version at the bottom of the menu is the new one.

```bash
docker compose ps
```

```bash
curl -s https://t212ext.<your-domain>/health
```

**Changing only `.env`** needs no build: `docker compose up -d` recreates the container with the new values.

**Changing the domain:** a new DNS record, then edit `~/traceon/caddy-sites/t212ext.caddy` and validate before reloading, as in [DEPLOYMENT.md](DEPLOYMENT.md#8-https-through-the-shared-caddy). Never reload without validating: a broken site file would keep Caddy, and every site on the server, from starting again.

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
docker compose logs --tail 100 t212ext-web
```

```bash
docker compose logs -f --since 10m t212ext-web
```

Logs rotate at 5 files of 10 MB. Worth knowing:

- `Failed sign-in for <user> from <address>`: a wrong password or code. After 5 attempts a minute from one address the sign-in page asks to wait.
- `API key refused; remaining jobs skipped`: Trading 212 answered 401, so the key was revoked or expired; see section 5.
- `Sync job <job> failed for account <id>`: the **Sync log** page has the detail. A 403 there means the key lacks a permission ([T212-API.md](T212-API.md)).
- `asked for report <id>` and `imported report <id>`: the automatic CSV reports. Their list, with what each one added, is on the **Import CSV** page; `report <id> could not be imported` comes with the reason, and the period is asked for again on the next sync.

**Restart** (keeps everything):

```bash
cd ~/t212ext && docker compose restart t212ext-web
```

Memory and CPU of every container on the server:

```bash
docker stats --no-stream
```

## 4. Users and sign-in

Users are in `.env` (`Auth__Users__N__*`, [DEPLOYMENT.md](DEPLOYMENT.md#5-sign-in)). After any change, `docker compose up -d`.

- **Change a password:** a new hash with `docker compose run --rm -it t212ext-web hash-password`, into `Auth__Users__0__PasswordHash`. Every session of that user ends.
- **New authenticator (phone lost or replaced):** if the secret is in the password manager, add it to the new phone. Otherwise create a new one with `docker compose run --rm t212ext-web new-totp-secret <username>`. Changing it also ends the sessions.
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
| VAPID keys (notifications) | `Push__PublicKey`, `Push__PrivateKey` | Section 9. |
| Backup passphrase | `~/.config/t212ext/backup.passphrase` | Only for new backups: keep the old one in the password manager as long as backups made with it exist (5 weeks). |

If a secret leaks, change it first and investigate afterwards.

## 7. Backups and restore

**Nightly** `ops/backup/t212ext-backup.sh`, as the app's login, and **weekly** the restore test (cron lines in [DEPLOYMENT.md](DEPLOYMENT.md#10-backups-and-monitoring)): encrypted, 7 daily and 4 weekly copies in `/var/backups/t212ext` (files `T212Ext-*.bak.gz.gpg`), mirrored to the Storage Box folder `t212ext-backups`.

```bash
tail -n 5 ~/t212ext-backup.log
```

```bash
tail -n 20 ~/t212ext-restore-test.log
```

**Restore test by hand** (restores into a throwaway `T212Ext_RestoreTest`, compares row counts, drops it; the live database is only read). Now and then, test the **Storage Box copy**, the real disaster case:

```bash
mkdir -p /tmp/t212ext-restore && rsync -a -e "ssh -p 23 -i ~/.ssh/storagebox_ed25519" <u123456>@<u123456>.your-storagebox.de:t212ext-backups/daily/ /tmp/t212ext-restore/
```

```bash
~/t212ext/ops/backup/t212ext-restore-test.sh "$(ls -1t /tmp/t212ext-restore/T212Ext-* | head -n1)"
```

**Disaster restore** (replaces the live database; needs `sa`). On a new server, first write the passphrase from the password manager to `~/.config/t212ext/backup.passphrase` and fetch the file from the Storage Box (command above).

```bash
cd ~/t212ext && docker compose stop t212ext-web
```

```bash
gpg --batch --pinentry-mode loopback --passphrase-file ~/.config/t212ext/backup.passphrase -d <T212Ext-file.bak.gz.gpg> | gunzip > /tmp/t212ext-restore.bak
```

```bash
docker cp /tmp/t212ext-restore.bak traceon-db:/var/opt/mssql/backup/t212ext-restore.bak && rm /tmp/t212ext-restore.bak
```

```bash
sql "ALTER DATABASE [T212Ext] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [T212Ext] FROM DISK = N'/var/opt/mssql/backup/t212ext-restore.bak' WITH REPLACE; ALTER DATABASE [T212Ext] SET MULTI_USER;"
```

On a new server, run `~/t212ext/ops/db/create-database.sh` now: it maps the restored database's user to the new login. Then:

```bash
docker exec traceon-db rm -f /var/opt/mssql/backup/t212ext-restore.bak && cd ~/t212ext && docker compose start t212ext-web
```

What a backup does not hold: `.env` and `backup.env` (keep the secrets in the password manager) and the data protection keys (losing them only signs everyone out).

## 8. Database

Size, against SQL Server Express's 10 GB per database (years of history stay far below):

```bash
t212sql "SELECT SUM(size) * 8 / 1024 AS DataMB FROM sys.database_files WHERE type = 0;"
```

State (must be `ONLINE` and `SIMPLE`):

```bash
t212sql "SELECT CAST(DATABASEPROPERTYEX('T212Ext', 'Status') AS nvarchar(20)) AS State, CAST(DATABASEPROPERTYEX('T212Ext', 'Recovery') AS nvarchar(20)) AS Recovery;"
```

Read-only queries from your PC with SSMS: the infrastructure's SSH tunnel (traceon `docs/OPERATIONS.md`, Database), signed in as `t212ext` (password from `.env`), database `T212Ext`.

The SQL Server is shared: restarting it, changing its edition or upgrading it (in traceon's project) also stops T212Ext for that time. The app retries the database for about a minute at startup; while running, an action during the restart may fail, and works again once SQL Server is back.

## 9. Notifications

Set up in [DEPLOYMENT.md](DEPLOYMENT.md#11-notifications-optional). The **Notifications** page lists the devices with notifications on, when each got its last one, and removes them.

- **What is sent:** after each sync, the dividends that arrived (paid in the last 7 days, so a report's old rows are never announced); after the first sync of a month, the month before. The **Sync log** shows the job "Notifications" with how many devices took them.
- **A device stops getting them:** the push service told the server it no longer exists (the app was uninstalled, the browser's data cleared), and the server removed it. Turn them on again there.
- **Changing the VAPID keys** (if the private one leaked): `new-vapid-keys`, the two lines in `.env`, `docker compose up -d`. Every device then needs **Turn on here** again; the old subscriptions stop working, and the server removes them at the next notification.
- **Turning them off for everyone:** empty `Push__PublicKey` in `.env` and `docker compose up -d`. The devices stay listed and work again once the keys are back.
