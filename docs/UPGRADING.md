# Upgrading

Deploy steps for the versions that are not yet in production.

The usual deploy command and checks are in [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update).

## 2.4.0 — First deployment

There is no production yet: this version is the first one on the server. Follow [DEPLOYMENT.md](DEPLOYMENT.md) from the start.

- **Needs the shared infrastructure first:** the Docker networks `edge` (Caddy) and `data` (SQL Server), and Caddy reading `~/traceon/caddy-sites/`. They come with traceon's version that introduced them; check with the command in [DEPLOYMENT.md](DEPLOYMENT.md#2-before-you-start).
- **Settings:** everything in `~/t212ext/.env`, from `.env.example`: the database password, at least one user (`Auth__Users__0__*`), and the Trading 212 accounts.
- **Data:** the database starts empty. The first sync fills it; import the CSV reports for the older history, and enter stock splits and instrument settings again ([DEPLOYMENT.md](DEPLOYMENT.md#9-first-data)).
- **Backups:** the app's own scripts, a Storage Box folder and a passphrase of their own, and two cron lines ([DEPLOYMENT.md](DEPLOYMENT.md#10-backups-and-monitoring)). Store the passphrase in the password manager.
- **Try afterwards:** sign in and out, a wrong code (refused), "Sync now", the IRS report for last year, and the site on the phone.

## How this file works

- Each version section lists only what is extra to the usual deploy command: migrations to be aware of, new settings, and what to try afterwards.
- Versions are deployed in order, oldest first.
- Once production runs a version, its section is removed. The history stays in git and in [RELEASE-NOTES.md](RELEASE-NOTES.md).
- Database migrations apply on their own when the app starts.
