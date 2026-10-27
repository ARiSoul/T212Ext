# Upgrading

Deploy steps for the versions that are not yet in production.

The usual deploy command and checks are in [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update).

**Production runs 2.18.0** (since 2026-10-15).

## 3.0.0

The database moves from traceon's SQL Server to the app's own PostgreSQL (`t212ext-db`), and the data is copied over once. **The SQL Server database is only read: it stays, untouched, as the way back** until you confirm the move. Allow about 15 minutes; the app is stopped for most of them. Why and what changed: [POSTGRESQL.md](POSTGRESQL.md).

Run the steps in order, on the server, in `~/t212ext`.

**1. A last backup of the SQL Server database**, with 2.18.0's script (the pull below replaces it):

```bash
~/t212ext/ops/backup/t212ext-backup.sh
```

**2. Stop the app** (nothing writes to the old database from here on):

```bash
cd ~/t212ext && docker compose stop t212ext-web
```

**3. The code of 3.0.0:**

```bash
cd ~/t212ext && git pull --ff-only
```

**4. The new database password**, added to `.env` (it is never shown). Keep the `T212EXT_DB_PASSWORD` line: the copy and the way back need it.

```bash
cd ~/t212ext && echo "T212EXT_PG_PASSWORD=$(openssl rand -hex 32)" >> .env
```

**5. Build 3.0.0's image and start only the new database:**

```bash
cd ~/t212ext && export T212EXT_VERSION=3.0.0 && docker compose build t212ext-web && docker tag t212ext-web:3.0.0 t212ext-web:latest
```

```bash
docker compose up -d t212ext-db && docker compose ps t212ext-db
```

Wait until `t212ext-db` is `healthy` (a few seconds; run `docker compose ps t212ext-db` again).

**6. Copy the data.** It migrates the new database, copies every table in one transaction, and prints the row count of every table in both:

```bash
cd ~/t212ext && docker compose -f docker-compose.yml -f ops/postgres/copy-from-sqlserver.yml run --rm t212ext-copy
```

Expected: every table with the same count in both columns, and `Every table has the same number of rows.` Anything else ("DIFFERENT", an error): do not go on. Empty the new database and run step 6 again, or take the way back:

```bash
docker exec t212ext-db psql -U t212ext -d postgres -X -c "DROP DATABASE t212ext WITH (FORCE);" -c "CREATE DATABASE t212ext;"
```

**7. Start the app on PostgreSQL:**

```bash
cd ~/t212ext && docker compose up -d && docker compose ps
```

`t212ext-web` turns `healthy` after about a minute. It no longer joins the network `data`.

**8. Check** in the app: the version at the bottom of the menu is 3.0.0; the overview, dividends, calendar and IRS report show the same figures as before; the sync log shows the sync on startup; you are still signed in (the keys volume did not change).

**9. Backups** now read from `t212ext-db`. In `ops/backup/backup.env`, set `DB_CONTAINER="t212ext-db"` and remove the `SA_ENV_FILE` and `SA_ENV_KEY` lines (the new `backup.env.example` shows the end of the file):

```bash
nano ~/t212ext/ops/backup/backup.env
```

Then a backup and the restore test by hand. The cron lines stay as they are.

```bash
~/t212ext/ops/backup/t212ext-backup.sh
```

```bash
~/t212ext/ops/backup/t212ext-restore-test.sh
```

**10. From now on, deploy with the script** ([OPERATIONS.md](OPERATIONS.md#1-deploying-an-update)): it builds the version's image and keeps the three newest. Run it once now; the second command removes the image of the old builds (`t212ext-t212ext-web`, a name the script does not manage) and lists what is kept:

```bash
cd ~/t212ext && ./ops/deploy.sh
```

```bash
docker rmi t212ext-t212ext-web 2> /dev/null; docker images t212ext-web
```

**The way back** (before confirming): the old version reads the SQL Server database, which nothing changed since step 2. What the app wrote to PostgreSQL meanwhile (syncs, sign-ins, notifications' state) is not taken back; the next sync fetches the API's part again.

```bash
cd ~/t212ext && docker compose stop t212ext-web && git checkout v2.18.0 && docker compose up -d --build t212ext-web
```

Then look at what went wrong (the copy's output, `docker compose logs t212ext-web`). `git checkout main` returns to 3.0.0 later, the new database still there.

**Confirming the move** (after a few days of normal use, by you):

- Remove the `T212EXT_DB_PASSWORD` line from `.env` (`nano ~/t212ext/.env`).
- **traceon's side** (in a traceon session): drop the database `T212Ext` and the login `t212ext` from its SQL Server, and update traceon's `docs/OPERATIONS.md`, section 20: T212Ext no longer uses SQL Server nor the network `data`.
- The old backups (`T212Ext-*.bak.gz.gpg`, capital T) are no longer pruned by the new script, which keeps `t212ext-*.dump.gpg`. Once you no longer want them, here and on the Storage Box:

```bash
ls -1 /var/backups/t212ext/daily/T212Ext-* /var/backups/t212ext/weekly/T212Ext-*
```

```bash
rm -f /var/backups/t212ext/daily/T212Ext-* /var/backups/t212ext/weekly/T212Ext-* && ~/t212ext/ops/backup/t212ext-backup.sh
```

  (The backup's mirror, `rsync --delete`, then removes them from the Storage Box too.)
- A later version removes the copy command and the SQL Server package (backlog 4.13).

**Locally** (your PC, if you run the app there): start the development PostgreSQL (README, "Run"), then copy your LocalDB data into it with the same command:

```bash
dotnet run --project src/T212Ext.Web -- copy-from-sqlserver "--ConnectionStrings:SqlServer=Server=(localdb)\MSSQLLocalDB;Database=T212Ext;Trusted_Connection=True;TrustServerCertificate=True"
```

## How this file works

- Each version section lists only what is extra to the usual deploy command: migrations to be aware of, new settings, and what to try afterwards.
- A step that runs a command the version adds (`docker compose run … <command>`) comes after `git pull` and `docker compose build`: before that, the server's image is the old one, which does not know the command and starts the app instead.
- Versions are deployed in order, oldest first.
- Once production runs a version, its section is removed. The history stays in git and in [RELEASE-NOTES.md](RELEASE-NOTES.md).
- The "Production runs" line above changes in that same edit.
- Database migrations apply on their own when the app starts.
