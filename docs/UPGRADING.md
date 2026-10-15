# Upgrading

Deploy steps for the versions that are not yet in production.

The usual deploy command and checks are in [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update).

**Production runs 2.15.0** (since 2026-10-15).

## 2.16.0

- **Migration** `AddSignInAttempts`: one new table, applied when the app starts.
- **Try afterwards:** sign out and in again, then open "Sign-ins" (menu, Data): your sign-in is there, with your address and device. A wrong password tried once shows as "Wrong password or code".

## 2.17.0

- Only the usual deploy: no migration, no new setting.
- **Try afterwards:** reload with the browser's developer tools open (F12, Console): no "Content Security Policy" errors on the overview (charts), the calendar and the notifications page; the theme still applies at once. `curl -sI https://t212.arisoul.org/login | grep -i content-security` shows the policy.

## How this file works

- Each version section lists only what is extra to the usual deploy command: migrations to be aware of, new settings, and what to try afterwards.
- A step that runs a command the version adds (`docker compose run … <command>`) comes after `git pull` and `docker compose build`: before that, the server's image is the old one, which does not know the command and starts the app instead.
- Versions are deployed in order, oldest first.
- Once production runs a version, its section is removed. The history stays in git and in [RELEASE-NOTES.md](RELEASE-NOTES.md).
- The "Production runs" line above changes in that same edit.
- Database migrations apply on their own when the app starts.
