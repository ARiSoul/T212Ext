# Upgrading

Deploy steps for the versions that are not yet in production.

The usual deploy command and checks are in [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update).

**Production runs 2.7.0** (since 2026-10-15).

## 2.7.1

- Only the usual deploy.
- **Try afterwards:** `docker compose run --rm t212ext-web no-such-command` must answer "Unknown command" and stop.

## 2.8.0

- Only the usual deploy: no migration, no new setting.
- **Try afterwards:** the IRS report for 2025: the quadro 8A lines show "Withheld", "Treaty" and "Tax paid abroad"; with a dividend above its treaty, the switch changes the last one. On the capital gains tab, with holdings under a year, the aggregation check.

## 2.9.0

- Only the usual deploy: no migration, no new setting.
- **Try afterwards:** on the dividends page, hide a column, filter a range of dates, save it as a view, reset, and open the view again.

## 2.10.0

- **Migration** `AddInstrumentLogos`: one new table, applied when the app starts.
- **Optional setting:** `Logos__Enabled=false` in `.env` to show only avatars (no fetching).
- **Try afterwards:** the dividends and positions pages show the logos; the first visit fetches them, so they may appear a moment later.

## How this file works

- Each version section lists only what is extra to the usual deploy command: migrations to be aware of, new settings, and what to try afterwards.
- A step that runs a command the version adds (`docker compose run … <command>`) comes after `git pull` and `docker compose build`: before that, the server's image is the old one, which does not know the command and starts the app instead.
- Versions are deployed in order, oldest first.
- Once production runs a version, its section is removed. The history stays in git and in [RELEASE-NOTES.md](RELEASE-NOTES.md).
- The "Production runs" line above changes in that same edit.
- Database migrations apply on their own when the app starts.
