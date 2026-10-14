# Upgrading

Deploy steps for the versions that are not yet in production.

The usual deploy command and checks are in [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update).

**Production runs 2.4.1** (since 2026-10-14).

## 2.5.0

- **Migration** `AddAccountReports`: one new table, applied when the app starts.
- **No new setting needed:** automatic reports are on by default (`Reports__Enabled=false` in `.env` turns them off).
- **Try afterwards:** "Sync now", then the **Import CSV** page:
  - the first report shows as waiting or imported; the first sync asks for the reports one year at a time, from the account's first activity, and the next syncs finish what is left;
  - in the **Sync log**, the job "CSV reports" must succeed. A 403 there means the key lacks a permission for reports: note which one Trading 212 names, for [T212-API.md](T212-API.md).
- **Check for duplicates on the first reports.** The history was already imported by hand, so for those periods a report should only *complete* dividends ("0 new, N completed"). Many *new* dividends there mean the API's reports are written differently from the app's (CSV dividends have no ID and are recognised by their values as written): turn reports off (`Reports__Enabled=false`, `docker compose up -d`), restore the backup taken before the deploy ([OPERATIONS.md](OPERATIONS.md#7-backups-and-restore)), and tell the maintainer.
- **Expect** the first syncs to take a few minutes longer, while the history is covered.

## How this file works

- Each version section lists only what is extra to the usual deploy command: migrations to be aware of, new settings, and what to try afterwards.
- Versions are deployed in order, oldest first.
- Once production runs a version, its section is removed. The history stays in git and in [RELEASE-NOTES.md](RELEASE-NOTES.md).
- The "Production runs" line above changes in that same edit.
- Database migrations apply on their own when the app starts.
