# Deployment

First-time setup of T212Ext on the Hetzner server that already runs traceon. Day-to-day work (updates, backups, logs, users) is in [OPERATIONS.md](OPERATIONS.md).

## Contents

1. [How it fits next to traceon](#1-how-it-fits-next-to-traceon)
2. [Before you start](#2-before-you-start)
3. [Get the code and the settings file](#3-get-the-code-and-the-settings-file)
4. [Database](#4-database)
5. [Sign-in](#5-sign-in)
6. [Trading 212 API key](#6-trading-212-api-key)
7. [Start it](#7-start-it)
8. [HTTPS through traceon's Caddy](#8-https-through-traceons-caddy)
9. [First data](#9-first-data)
10. [Backups and monitoring](#10-backups-and-monitoring)

## 1. How it fits next to traceon

```
Internet ──443──▶ traceon's Caddy ──▶ t212ext:8080 ──▶ sqlserver (traceon-db), database T212Ext
                  (traceon-caddy)     (this app)        own login "t212ext"
```

- **One container, `t212ext`**, built from this repository's `Dockerfile` by its `docker-compose.yml`. It publishes no port.
- **It joins traceon's Docker network** (`traceon_default`): traceon's Caddy reaches it as `t212ext:8080`, and it reaches traceon's SQL Server as `sqlserver`.
- **HTTPS is traceon's Caddy**, with one more site file (section 8). Certificates are automatic, as for traceon.
- **The database `T212Ext` lives on traceon's SQL Server Express**, with its own login that can touch nothing else. No second SQL Server, so no extra memory.
- **Backups** reuse traceon's script with `DB_NAME=T212Ext` (section 10).
- **Sign-in** is the app's own: users with a password and, preferably, an authenticator code, set in `.env`. Every page needs it; only `/login` and `/health` are open.
- **Data protection keys** (they encrypt the sign-in and language cookies) are on a Docker volume, so restarts and new images do not sign you out.

## 2. Before you start

- traceon running on the server in `~/traceon` (its `docs/DEPLOYMENT.md`), as the `deploy` user over SSH.
- **traceon's Caddy reads extra site files from `~/traceon/caddy-sites/`.** If `~/traceon/Caddyfile` has no `import /etc/caddy/sites/*.caddy` line, update traceon first.
- **A domain for the app**, for example `dividends.<your-domain>`, with an `A` record (and `AAAA` if the server has IPv6) to the server's address. Check it before section 8:

```bash
dig +short dividends.<your-domain>
```

- An **authenticator app** on your phone (any TOTP app: Google Authenticator, Microsoft Authenticator, 1Password, Bitwarden, Aegis).

## 3. Get the code and the settings file

```bash
cd ~ && git clone https://github.com/ARiSoul/T212Ext.git t212ext
```

```bash
cd ~/t212ext && cp .env.example .env && chmod 600 .env
```

`.env` holds every secret of the app and is not committed. As with traceon, the server's working tree has no other local changes: `git status` shows nothing modified.

## 4. Database

Generate the database password straight into `.env` (it is never shown):

```bash
cd ~/t212ext && sed -i "s|^T212EXT_DB_PASSWORD=.*|T212EXT_DB_PASSWORD=$(openssl rand -base64 33 | tr -d '/+=\n')|" .env
```

Create the database and its login on traceon's SQL Server. The script reads the `sa` password from `~/traceon/.env` and the new one from `~/t212ext/.env`, and passes both through the environment, never on a command line:

```bash
~/t212ext/ops/db/create-database.sh
```

It ends with a line `T212Ext  SIMPLE  ONLINE`. It is safe to run again; it also resets the login's password to the one in `.env` (that is how the password is changed, see [OPERATIONS.md](OPERATIONS.md#6-changing-secrets)).

What it does ([ops/db/create-database.sql](../ops/db/create-database.sql)): creates the database `T212Ext` with the simple recovery model (nightly full backups, no log backups), a login `t212ext`, and makes it owner of that database only. The app applies its migrations itself when it starts.

## 5. Sign-in

Choose a username and write it in `.env` (`Auth__Users__0__Username=`):

```bash
nano ~/t212ext/.env
```

**Password.** The app stores only a hash. This builds the image the first time (a few minutes), asks for the password twice without showing it, and prints the hash:

```bash
cd ~/t212ext && docker compose run --rm -it t212ext hash-password
```

Put the printed line in `Auth__Users__0__PasswordHash=`. Use a long password from the password manager (at least 12 characters).

**Authenticator code (recommended).** The app holds your whole portfolio and tax history; with a code, a leaked password is not enough:

```bash
cd ~/t212ext && docker compose run --rm t212ext new-totp-secret <username>
```

It prints a secret (32 letters and digits) and an `otpauth://` link. Put the secret in `Auth__Users__0__TotpSecret=`, and add it to the authenticator app as a "setup key" (time-based). Keep a copy in the password manager: it is the only way to recover the codes if the phone is lost.

`docker compose run` needs `T212EXT_DB_PASSWORD` already in `.env` (section 4), since compose reads the whole file.

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

`t212ext` turns `healthy` after about a minute. The first start creates the tables (migrations), then the first sync starts.

```bash
docker compose logs --tail 50 t212ext
```

Expected: `Now listening on: http://[::]:8080`, then the sync jobs. Errors about the database mean section 4 (or `T212EXT_DB_PASSWORD` changed without running the script); an error about `Auth:Users` means section 5.

**If the network is not found** (`network traceon_default declared as external, but could not be found`): traceon's project has another name. List the networks and set `TRACEON_NETWORK=<name>` in `.env`:

```bash
docker network ls --filter name=traceon
```

## 8. HTTPS through traceon's Caddy

The site file comes from [ops/caddy/t212ext.caddy.example](../ops/caddy/t212ext.caddy.example):

```bash
cp ~/t212ext/ops/caddy/t212ext.caddy.example ~/traceon/caddy-sites/t212ext.caddy && nano ~/traceon/caddy-sites/t212ext.caddy
```

Replace `dividends.example.com` with your domain. Then reload Caddy (no downtime for traceon):

```bash
docker exec traceon-caddy caddy reload --config /etc/caddy/Caddyfile
```

Caddy gets the certificate within a few seconds. Check:

```bash
curl -s https://dividends.<your-domain>/health
```

It must answer `Healthy`. Then open `https://dividends.<your-domain>` and sign in.

## 9. First data

The sync on startup fetches what the API gives (positions, orders, dividends and cash movements). The older history comes from the CSV reports:

1. In Trading 212: **History → Export**, one file per 12 months, back to the account's start.
2. In the app: **Import CSV**, all files at once.
3. **Stock splits** (menu IRS report → Capital gains, when it reports missing shares) and **instrument settings** (source country) are entered by hand again.

**Moving the local database instead is not possible:** LocalDB on Windows is SQL Server 2025 (version 17), and a backup cannot be restored on an older SQL Server (the server runs 2022). Everything except splits and instrument settings comes back from the API and the CSV reports.

## 10. Backups and monitoring

**Nightly backup.** traceon's backup script backs up any database with `DB_NAME`: same encryption, same retention (7 daily, 4 weekly) and the same Storage Box. Add a second line to the crontab, a quarter of an hour after traceon's:

```bash
crontab -e
```

```
45 3 * * * DB_NAME=T212Ext /home/deploy/traceon/ops/backup/traceon-backup.sh >> /home/deploy/t212ext-backup.log 2>&1
```

Run it once by hand and check the restore:

```bash
DB_NAME=T212Ext ~/traceon/ops/backup/traceon-backup.sh
```

```bash
DB_NAME=T212Ext ~/traceon/ops/backup/traceon-restore-test.sh
```

Both pings go to the same dead man's switch as traceon's (`PING_URL` in traceon's `backup.env`): a failure of either sends the alert.

**Monitoring.** In the same uptime service as traceon, a keyword monitor on `https://dividends.<your-domain>/health`, keyword `Healthy`, every 5 minutes.

Done. From now on, updates follow [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update), and each version's extra steps are in [UPGRADING.md](UPGRADING.md).
