# Upgrading

Deploy steps for the versions that are not yet in production.

The usual deploy command and checks are in [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update).

**Production runs 2.10.0** (since 2026-10-15).

## 2.11.0

- Only the usual deploy: no migration, no new setting.
- **Try afterwards:** the dividends page in Portuguese shows translated types; an American instrument's page has "Abrir na Trading 212", which opens its page there.

## 2.12.0

- Only the usual deploy: no migration, no new setting.
- **Try afterwards:** an instrument held for some years (its page, "Dividend per share, by year"), and the growth columns on "Income by instrument".

## 2.13.0

- **Migration:** `AddGoals` adds the `Goals` table; it applies on its own when the app starts.
- **Try afterwards:** "Goals" in the menu: add one (for example 50 % of a monthly bill) and check the progress and the chart.

## 2.14.0

- Only the usual deploy: no migration, no new setting.
- **Try afterwards:** "Calendar" in the menu: the next payments should look like last year's, a year later. The estimate for the next 12 months on the overview can move a little (special dividends and holdings that stopped paying are now left out).

## How this file works

- Each version section lists only what is extra to the usual deploy command: migrations to be aware of, new settings, and what to try afterwards.
- A step that runs a command the version adds (`docker compose run … <command>`) comes after `git pull` and `docker compose build`: before that, the server's image is the old one, which does not know the command and starts the app instead.
- Versions are deployed in order, oldest first.
- Once production runs a version, its section is removed. The history stays in git and in [RELEASE-NOTES.md](RELEASE-NOTES.md).
- The "Production runs" line above changes in that same edit.
- Database migrations apply on their own when the app starts.
