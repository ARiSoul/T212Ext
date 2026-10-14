# Deployment

First-time setup of T212Ext on the Hetzner server that also runs traceon. Day-to-day work (updates, backups, logs, users) is in [OPERATIONS.md](OPERATIONS.md).

## Contents

1. [How it fits on the server](#1-how-it-fits-on-the-server)
2. [Before you start](#2-before-you-start)
3. [Get the code and the settings file](#3-get-the-code-and-the-settings-file)
4. [Database](#4-database)
5. [Sign-in](#5-sign-in)
6. [Trading 212 API key](#6-trading-212-api-key)
7. [Start it](#7-start-it)
8. [HTTPS through the shared Caddy](#8-https-through-the-shared-caddy)
9. [First data](#9-first-data)
10. [Backups and monitoring](#10-backups-and-monitoring)

## 1. How it fits on the server

T212Ext and traceon are independent apps, each its own Docker Compose project. They share only the server's infrastructure: the reverse proxy, which alone can own ports 80 and 443, and SQL Server, so the small server runs one instance (SQL Server on Linux needs 2 GB of memory each). That infrastructure lives in traceon's compose project today; T212Ext reaches it only through two neutral Docker networks, never traceon's own.

```
browser ──▶ t212ext.<your-domain> ──▶ server :443 ──▶ Caddy (shared)
                                                        │  network edge
                                                        ▼
                                                  t212ext-web:8080
                                                        │  network data
                                                        ▼
                                                  SQL Server "sqlserver" (shared)
                                                  database T212Ext, login "t212ext"
```

traceon's domains go through the same Caddy to traceon's containers, on traceon's own network, which T212Ext cannot see.

- **One container, `t212ext-web`**, built from this repository's `Dockerfile` by its `docker-compose.yml`. It publishes no port.
- **Network `edge`:** the proxy and the apps it serves. Caddy reaches the app as `t212ext-web:8080`. Caddy picks the app by domain; each domain gets its own certificate automatically.
- **Names on the shared networks are DNS names**, so they carry the app's prefix (`t212ext-web`), never a generic one (`web`, `api`) nor `sqlserver`, `caddy` or `traceon-*`.
- **Network `data`:** the apps and SQL Server, reached as `sqlserver`. T212Ext has its own database and a login that can touch nothing else.
- **T212Ext cannot reach traceon's containers**, nor traceon T212Ext's. Different domains also mean the browser never mixes their cookies.
- **Backups are the app's own** ([ops/backup](../ops/backup)): signed in with the app's login, to a folder of their own on the Storage Box.
- **The rules of that shared infrastructure** (what it keeps stable, and what every app does on its side) are in traceon's `docs/OPERATIONS.md`, section 20, "Other apps on this server".
- **Sign-in** is the app's own: users with a password and, preferably, an authenticator code, set in `.env`. Every page needs it; only `/login` and `/health` are open.
- **Data protection keys** (they encrypt the sign-in and language cookies) are on a Docker volume, so restarts and new images do not sign you out.

## 2. Before you start

- The server, with the `deploy` user over SSH, and traceon running in `~/traceon` (its `docs/DEPLOYMENT.md`).
- **The shared infrastructure:** the networks `edge` and `data` exist, Caddy is on `edge` and reads site files from `~/traceon/caddy-sites/`, and SQL Server is on `data`. These come with traceon's version that introduced them (its `UPGRADING.md`). Check:

```bash
docker network inspect edge data --format '{{.Name}}: {{range .Containers}}{{.Name}} {{end}}'
```

  Expected: `edge: traceon-caddy` and `data: traceon-db`. If the networks are missing, update traceon first.
- **A domain for the app**, for example `t212ext.<your-domain>`, with an `A` record (and `AAAA` if the server has IPv6) to the server's address. Check it before section 8:

```bash
dig +short t212ext.<your-domain>
```

- An **authenticator app** on your phone (any TOTP app: Google Authenticator, Microsoft Authenticator, 1Password, Bitwarden, Aegis).

## 3. Get the code and the settings file

```bash
cd ~ && git clone https://github.com/ARiSoul/T212Ext.git t212ext
```

```bash
cd ~/t212ext && cp .env.example .env && chmod 600 .env
```

`.env` holds every secret of the app and is not committed. The server's working tree has no other local changes: `git status` shows nothing modified.

## 4. Database

Generate the database password straight into `.env` (it is never shown):

```bash
cd ~/t212ext && sed -i "s|^T212EXT_DB_PASSWORD=.*|T212EXT_DB_PASSWORD=$(openssl rand -base64 33 | tr -d '/+=\n')|" .env
```

Create the database and its login. Creating a database needs SQL Server's `sa`, so the script reads its password from where the infrastructure keeps it (`~/traceon/.env` by default; `SA_ENV_FILE`, `SA_ENV_KEY` and `DB_CONTAINER` change that) and the new one from `~/t212ext/.env`, and passes both through the environment, never on a command line:

```bash
~/t212ext/ops/db/create-database.sh
```

It ends with a line `T212Ext  SIMPLE  ONLINE`. It is safe to run again; it also resets the login's password to the one in `.env` (that is how the password is changed, see [OPERATIONS.md](OPERATIONS.md#6-changing-secrets)).

What it does ([ops/db/create-database.sql](../ops/db/create-database.sql)): creates the database `T212Ext` with the simple recovery model (nightly full backups, no log backups), a login `t212ext`, and makes it owner of that database only. The app applies its migrations itself when it starts, and the nightly backup uses the same login: after this step, nothing of T212Ext needs `sa` except the weekly restore test.

## 5. Sign-in

Choose a username and write it in `.env` (`Auth__Users__0__Username=`):

```bash
nano ~/t212ext/.env
```

**Password.** The app stores only a hash. This builds the image the first time (a few minutes), asks for the password twice without showing it, and prints the hash:

```bash
cd ~/t212ext && docker compose run --rm -it t212ext-web hash-password
```

Put the printed line in `Auth__Users__0__PasswordHash=`. Use a long password from the password manager (at least 12 characters).

**Authenticator code (recommended).** The app holds your whole portfolio and tax history; with a code, a leaked password is not enough:

```bash
cd ~/t212ext && docker compose run --rm t212ext-web new-totp-secret <username>
```

It prints a secret (32 letters and digits) and an `otpauth://` link. Put the secret in `Auth__Users__0__TotpSecret=`, and add it to the authenticator app as a "setup key" (time-based). Keep a copy in the password manager: it is the only way to recover the codes if the phone is lost.

`docker compose run` needs `T212EXT_DB_PASSWORD` already in `.env` (section 4), since compose reads the whole file, and the networks of section 2.

A second person gets a second block (`Auth__Users__1__Username`, …).

## 6. Trading 212 API key

Create a key for the server as in the [README](../README.md#1-create-a-trading-212-api-key): **Settings → API (Beta) → Generate API key**, read permissions only.

- Better a new key than the one of your local copy: each can be revoked on its own.
- **Restrict it to the server's IP address.** The server's address:

```bash
curl -4 -s https://ifconfig.me
```

- Copy the key and the secret into `.env` (`T212__Accounts__0__ApiKey`, `T212__Accounts__0__ApiSecret`). An older key without a secret works with `ApiSecret` empty.
- Each account is a block: `T212__Accounts__1__Key=isa`, `…__Name`, `…__ApiKey`, `…__ApiSecret`. The `Key` names the account in the database: never change it afterwards.

Details of the API in [T212-API.md](T212-API.md).

## 7. Start it

```bash
cd ~/t212ext && docker compose up -d --build
```

```bash
docker compose ps
```

`t212ext-web` turns `healthy` after about a minute. The first start creates the tables (migrations), then the first sync starts.

```bash
docker compose logs --tail 50 t212ext-web
```

Expected: `Now listening on: http://[::]:8080`, then the sync jobs. Errors about the database mean section 4 (or `T212EXT_DB_PASSWORD` changed without running the script); an error about `Auth:Users` means section 5; `network edge declared as external, but could not be found` means section 2.

## 8. HTTPS through the shared Caddy

The site file comes from [ops/caddy/t212ext.caddy.example](../ops/caddy/t212ext.caddy.example):

```bash
cp ~/t212ext/ops/caddy/t212ext.caddy.example ~/traceon/caddy-sites/t212ext.caddy && nano ~/traceon/caddy-sites/t212ext.caddy
```

Replace `t212ext.example.com` with your domain, written in the file itself: Caddy there has only traceon's environment variables, so `{$VAR}` would not work.

Validate the whole configuration, then reload (no downtime for the other sites). Always both, in this order:

```bash
docker exec traceon-caddy caddy validate --config /etc/caddy/Caddyfile && docker exec traceon-caddy caddy reload --config /etc/caddy/Caddyfile
```

**If the validation fails, fix or remove `t212ext.caddy` at once.** Caddy keeps running with the old configuration, but while a broken file is in the folder it would not start again after a restart or a traceon deploy, and every site on the server would be down, traceon's included.

Caddy gets the certificate within a few seconds. Check:

```bash
curl -s https://t212ext.<your-domain>/health
```

It must answer `Healthy`. Then open `https://t212ext.<your-domain>` and sign in.

## 9. First data

The sync on startup fetches what the API gives (positions, orders, dividends and cash movements). The older history comes from the CSV reports:

1. In Trading 212: **History → Export**, one file per 12 months, back to the account's start.
2. In the app: **Import CSV**, all files at once.
3. **Stock splits** (menu IRS report → Capital gains, when it reports missing shares) and **instrument settings** (source country) are entered by hand again.

**Moving the local database instead is not possible:** LocalDB on Windows is SQL Server 2025 (version 17), and a backup cannot be restored on an older SQL Server (the server runs 2022). Everything except splits and instrument settings comes back from the API and the CSV reports.

## 10. Backups and monitoring

The scripts are in [ops/backup](../ops/backup): `t212ext-backup.sh` every night, `t212ext-restore-test.sh` every week. Encrypted (gpg, AES256), 7 daily and 4 weekly copies, mirrored to a Hetzner Storage Box folder of their own. The Storage Box and its SSH key can be the ones traceon's backups already use (traceon `docs/OPERATIONS.md`, Backups); the folder and the passphrase are T212Ext's.

**Storage Box folder** (with the existing key, `~/.ssh/storagebox_ed25519`):

```bash
ssh -p 23 -i ~/.ssh/storagebox_ed25519 <u123456>@<u123456>.your-storagebox.de mkdir t212ext-backups
```

**Configuration** (`backup.env` is not committed). Fill in `REMOTE`; the rest has working defaults:

```bash
cd ~/t212ext && cp ops/backup/backup.env.example ops/backup/backup.env && nano ops/backup/backup.env
```

**Passphrase:**

```bash
mkdir -p ~/.config/t212ext && chmod 700 ~/.config/t212ext && openssl rand -base64 32 > ~/.config/t212ext/backup.passphrase && chmod 600 ~/.config/t212ext/backup.passphrase
```

Show it once and store it in the password manager as "T212Ext backup passphrase". **Without a copy outside the server, the backups cannot be read if the server is lost.**

```bash
cat ~/.config/t212ext/backup.passphrase
```

**Local folder:**

```bash
sudo mkdir -p /var/backups/t212ext && sudo chown deploy:deploy /var/backups/t212ext
```

**First run by hand**, then the restore test (it restores into a throwaway database, compares row counts with the live one and drops it):

```bash
~/t212ext/ops/backup/t212ext-backup.sh
```

```bash
~/t212ext/ops/backup/t212ext-restore-test.sh
```

**Cron:**

```bash
crontab -e
```

Nightly backup at 03:45 UTC, and the restore test on Sundays at 04:15:

```
45 3 * * * /home/deploy/t212ext/ops/backup/t212ext-backup.sh >> /home/deploy/t212ext-backup.log 2>&1
15 4 * * 0 /home/deploy/t212ext/ops/backup/t212ext-restore-test.sh >> /home/deploy/t212ext-restore-test.log 2>&1
```

**Dead man's switch (optional):** a check of its own on healthchecks.io (period 1 day, grace 2 hours), its ping URL in `PING_URL` in `backup.env`. The script pings it on success and `<PING_URL>/fail` on error.

**Monitoring.** In the uptime service, a keyword monitor on `https://t212ext.<your-domain>/health`, keyword `Healthy`, every 5 minutes.

Done. From now on, updates follow [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update), and each version's extra steps are in [UPGRADING.md](UPGRADING.md).
