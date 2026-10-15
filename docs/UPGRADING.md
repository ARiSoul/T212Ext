# Upgrading

Deploy steps for the versions that are not yet in production.

The usual deploy command and checks are in [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update).

**Production runs 2.7.0** (since 2026-10-15).

## 2.7.1

- Only the usual deploy.
- **Try afterwards:** `docker compose run --rm t212ext-web no-such-command` must answer "Unknown command" and stop.

## How this file works

- Each version section lists only what is extra to the usual deploy command: migrations to be aware of, new settings, and what to try afterwards.
- A step that runs a command the version adds (`docker compose run … <command>`) comes after `git pull` and `docker compose build`: before that, the server's image is the old one, which does not know the command and starts the app instead.
- Versions are deployed in order, oldest first.
- Once production runs a version, its section is removed. The history stays in git and in [RELEASE-NOTES.md](RELEASE-NOTES.md).
- The "Production runs" line above changes in that same edit.
- Database migrations apply on their own when the app starts.
