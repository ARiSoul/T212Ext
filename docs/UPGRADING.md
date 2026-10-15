# Upgrading

Deploy steps for the versions that are not yet in production.

The usual deploy command and checks are in [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update).

**Production runs 2.17.1** (since 2026-10-15).

## 2.18.0

- **Migration** `AddNotificationChoices`: new columns on the devices (all kinds on for the existing ones) and the week's state; applied when the app starts.
- **Try afterwards:** on the phone, the Notifications page: "On this device" shows the three kinds on; turn one off, set quiet hours, save, and the list shows them. Next Monday morning, the week's notice (if anything is expected).

## How this file works

- Each version section lists only what is extra to the usual deploy command: migrations to be aware of, new settings, and what to try afterwards.
- A step that runs a command the version adds (`docker compose run … <command>`) comes after `git pull` and `docker compose build`: before that, the server's image is the old one, which does not know the command and starts the app instead.
- Versions are deployed in order, oldest first.
- Once production runs a version, its section is removed. The history stays in git and in [RELEASE-NOTES.md](RELEASE-NOTES.md).
- The "Production runs" line above changes in that same edit.
- Database migrations apply on their own when the app starts.
