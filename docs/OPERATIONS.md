# Operations

Running T212Ext on the server once it is set up ([DEPLOYMENT.md](DEPLOYMENT.md)). The only shared infrastructure, Caddy and the network `edge`, lives in traceon's compose project: its `docs/OPERATIONS.md` covers it, and its section "Other apps on this server" says what it keeps stable for T212Ext. The database (`t212ext-db`, PostgreSQL) is T212Ext's own ([POSTGRESQL.md](POSTGRESQL.md)).

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
- **Never `export T212EXT_VERSION`** in the shell. It picks the image of every later `docker compose` command in that session (`up -d` would go back to that version, `run` would use its commands); give it only in front of one command, as in section 2. `docker compose config --images` shows the image compose would use. `./ops/deploy.sh` sets its own. *(2026-10-27: an `export` left from the 3.0.0 move made `docker compose run` use the 3.0.0 image after 3.1.0 was deployed.)*
- `t212psql -c "..."` runs SQL in the `t212ext` database, inside its container (the image trusts its local socket: no password). Without `-c` it opens `psql`. Define it in each SSH session, or append it once to `~/.bashrc`:

```bash
t212psql() { docker exec -i t212ext-db psql -U t212ext -d t212ext -X "$@"; }
```

  Names are quoted, as EF creates them: `t212psql -c 'SELECT COUNT(*) FROM "Dividends";'`.

## 1. Deploying an update

After a PR is merged to `main` (and its tag created):

1. Read the version's section in [UPGRADING.md](UPGRADING.md), if there is one.
2. Take a backup:

```bash
~/t212ext/ops/backup/t212ext-backup.sh
```

3. Pull, build and restart:

```bash
cd ~/t212ext && ./ops/deploy.sh
```

   It pulls `main`, builds the version's image (`t212ext-web:<version>`, also tagged `latest`), and restarts on it. **The three newest versions' images stay** for a rollback without a build; older ones, dangling layers and build cache older than a week are removed, as in traceon's deploy (the server's disk filled up once, 2026-10-25). The volumes stay; migrations apply when the app starts.

4. Check: `t212ext-web` is `healthy` after about a minute, `/health` answers, the version at the bottom of the menu is the new one.

```bash
docker compose ps
```

```bash
curl -s https://t212ext.<your-domain>/health
```

**Changing only `.env`** needs no build: `docker compose up -d` recreates the container with the new values.

**A command the new version adds** (such as `new-vapid-keys` in 2.7.0) needs the new image first: `docker compose run` uses the image tagged `latest`, and an older one does not know the command (from 2.7.1 on it refuses it with the list of the known ones). Deploy first (`./ops/deploy.sh`), then run the command.

**Changing the domain:** a new DNS record, then edit `~/traceon/caddy-sites/t212ext.caddy` and validate before reloading, as in [DEPLOYMENT.md](DEPLOYMENT.md#8-https-through-the-shared-caddy). Never reload without validating: a broken site file would keep Caddy, and every site on the server, from starting again.

A sync cut short by the restart keeps what it saved (history jobs save page by page), and the sync on startup carries on.

## 2. Rolling back

- **Code, to one of the three versions kept** (`docker images t212ext-web` lists them), here 3.0.0:

```bash
cd ~/t212ext && git checkout v3.0.0 && T212EXT_VERSION=3.0.0 docker compose up -d --no-build && docker tag t212ext-web:3.0.0 t212ext-web:latest
```

  An older version, whose image is gone: `git checkout v<version> && T212EXT_VERSION=<version> docker compose up -d --build`. Migrations are not reverted. Additive ones (new tables, new nullable columns) do not bother the previous version; otherwise restore the backup taken before the deploy (section 7). Return with `git checkout main && ./ops/deploy.sh`.
- **To 2.x** (before PostgreSQL): no longer possible. The SQL Server database was dropped once the move was confirmed (2026-10-27).
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

- `Failed sign-in for <user> from <address>`: a wrong password or code. After 5 attempts a minute from one address the sign-in page asks to wait. The **Sign-ins** page keeps every attempt of the last 90 days (section 4).
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

**Sign-ins page** (menu, Data): every attempt of the last 90 days, with the address and the device, and your previous sign-in. A sign-in you do not recognise: change the password (above), which ends every session, and check the Trading 212 keys (section 5). Many failures from one address are refused by the rate limit; for a lasting flood, block the address in Caddy.

## 5. Trading 212 accounts

- **New account:** a new block in `.env` (`T212__Accounts__1__Key`, `__Name`, `__Environment`, `__ApiKey`, `__ApiSecret`), then `docker compose up -d`. The first sync of that account runs at once.
- **Replace a key** (revoked, leaked, expired): generate a new one in Trading 212 (restricted to the server's IP), put it in `.env`, `docker compose up -d`, then delete the old key in Trading 212.
- **Never change an account's `Key`:** the database knows the account by it. Its `Name` can change freely.

## 6. Changing secrets

| Secret | Where | How |
|---|---|---|
| Database password | `T212EXT_PG_PASSWORD` | The database keeps the password it was created with. Generate a new one in `.env` (the `sed` line in [DEPLOYMENT.md](DEPLOYMENT.md#4-database)), give it to the database, then restart the app (below). |
| Password, authenticator secret | `Auth__Users__N__*` | Section 4. |
| Trading 212 key and secret | `T212__Accounts__N__*` | Section 5. |
| Data protection keys | volume `t212ext_keys` | Section 4, "sign out everywhere". Never in `.env`. |
| VAPID keys (notifications) | `Push__PublicKey`, `Push__PrivateKey` | Section 9. |
| Backup passphrase | `~/.config/t212ext/backup.passphrase` | Only for new backups: keep the old one in the password manager as long as backups made with it exist (5 weeks). |

If a secret leaks, change it first and investigate afterwards.

**Changing the database password.** After the new value is in `.env`, this sets it in the database. The value goes by environment and `psql` reads it with `\getenv`, so it is never on a command line:

```bash
cd ~/t212ext && PW="$(grep -E '^T212EXT_PG_PASSWORD=' .env | cut -d= -f2-)" && export PW && printf '%s\n' '\getenv pw PW' "ALTER USER t212ext WITH PASSWORD :'pw';" | docker exec -i -e PW t212ext-db psql -U t212ext -d t212ext -X -q && unset PW
```

```bash
docker compose up -d
```

## 7. Backups and restore

**Nightly** `ops/backup/t212ext-backup.sh` (`pg_dump`, checked with `pg_restore --list`), and **weekly** the restore test (cron lines in [DEPLOYMENT.md](DEPLOYMENT.md#10-backups-and-monitoring)): encrypted, 7 daily and 4 weekly copies in `/var/backups/t212ext` (files `t212ext-*.dump.gpg`), mirrored to the Storage Box folder `t212ext-backups`.

```bash
tail -n 5 ~/t212ext-backup.log
```

```bash
tail -n 20 ~/t212ext-restore-test.log
```

**Restore test by hand** (restores into a throwaway `t212ext_restore_test`, compares row counts, drops it; the live database is only read). Now and then, test the **Storage Box copy**, the real disaster case:

```bash
mkdir -p /tmp/t212ext-restore && rsync -a -e "ssh -p 23 -i ~/.ssh/storagebox_ed25519" <u123456>@<u123456>.your-storagebox.de:t212ext-backups/daily/ /tmp/t212ext-restore/
```

```bash
~/t212ext/ops/backup/t212ext-restore-test.sh "$(ls -1t /tmp/t212ext-restore/t212ext-* | head -n1)"
```

**Disaster restore** (replaces the live database). On a new server, first write the passphrase from the password manager to `~/.config/t212ext/backup.passphrase`, fetch the file from the Storage Box (command above), and start the database once (`docker compose up -d t212ext-db`).

```bash
cd ~/t212ext && docker compose stop t212ext-web
```

```bash
gpg --batch --pinentry-mode loopback --passphrase-file ~/.config/t212ext/backup.passphrase -d <t212ext-file.dump.gpg> > /tmp/t212ext-restore.dump
```

```bash
docker exec t212ext-db psql -U t212ext -d postgres -X -c "DROP DATABASE t212ext WITH (FORCE);" -c "CREATE DATABASE t212ext;"
```

```bash
docker exec -i t212ext-db pg_restore -U t212ext -d t212ext --no-owner --exit-on-error < /tmp/t212ext-restore.dump && rm /tmp/t212ext-restore.dump
```

```bash
docker compose start t212ext-web
```

What a backup does not hold: `.env` and `backup.env` (keep the secrets in the password manager) and the data protection keys (losing them only signs everyone out).

## 8. Database

`t212ext-db`, PostgreSQL, the project's own: restarting traceon (or anything in `~/traceon`) does not touch it. The app retries the database for about a minute at startup. Its decisions and lessons: [POSTGRESQL.md](POSTGRESQL.md).

Size and version:

```bash
t212psql -c "SELECT pg_size_pretty(pg_database_size('t212ext')) AS size, version();"
```

Rows per table:

```bash
t212psql -c "SELECT relname AS table, n_live_tup AS rows FROM pg_stat_user_tables ORDER BY relname;"
```

**Minor updates** (18.6 → 18.7: fixes, same data format). Nothing pulls the image again on its own; once a month, after a backup:

```bash
cd ~/t212ext && docker compose pull t212ext-db && docker compose up -d t212ext-db
```

**A new major version** (18 → 19) comes with a release that changes the image in `docker-compose.yml`, and is a dump and restore: the new version starts empty beside the old data (the volume keeps each major's data in its own folder, `/var/lib/postgresql/<major>/docker`), which stays as the way back. The release's UPGRADING section has the steps; in short:

1. `./ops/backup/t212ext-backup.sh`, and a plain dump: `docker exec t212ext-db pg_dump -U t212ext -d t212ext --format=custom > ~/t212ext-before-upgrade.dump`
2. `docker compose stop t212ext-web t212ext-db`, then `git pull` (the new image)
3. `docker compose up -d t212ext-db` (empty), then `docker exec -i t212ext-db pg_restore -U t212ext -d t212ext --no-owner --exit-on-error < ~/t212ext-before-upgrade.dump`
4. `./ops/deploy.sh`, check, and only later remove the old folder and the dump.

No port is published: queries go through `t212psql` on the server.

## 9. Notifications

Set up in [DEPLOYMENT.md](DEPLOYMENT.md#11-notifications-optional). The **Notifications** page lists the devices with notifications on, when each got its last one, and removes them.

- **What is sent:** after each sync, the dividends that arrived (paid in the last 7 days, so a report's old rows are never announced); after the first sync of a month, the month before; after the first sync from Monday 06:00 UTC, the payments the calendar expects that week (nothing expected, nothing sent). The **Sync log** shows the job "Notifications" with how many devices took them.
- **Choices per device:** on the Notifications page, with notifications on, "On this device" turns each kind on or off and sets quiet hours (sent without sound or vibration). The list shows what each device gets.
- **A device stops getting them:** the push service told the server it no longer exists (the app was uninstalled, the browser's data cleared), and the server removed it. Turn them on again there.
- **Changing the VAPID keys** (if the private one leaked): `new-vapid-keys`, the two lines in `.env`, `docker compose up -d`. Every device then needs **Turn on here** again; the old subscriptions stop working, and the server removes them at the next notification.
- **Turning them off for everyone:** empty `Push__PublicKey` in `.env` and `docker compose up -d`. The devices stay listed and work again once the keys are back.
