# Upgrading

Deploy steps for the versions that are not yet in production.

**No production server yet.** T212Ext runs locally until the deployment phase ([BACKLOG.md](BACKLOG.md#phase-4--deploy-next-to-traceon-23)). That phase adds the deploy command, `docs/DEPLOYMENT.md`, and the first section here.

## How this file works

- Each version section lists only what is extra to the usual deploy command: migrations to be aware of, new settings, and what to try afterwards.
- Versions are deployed in order, oldest first.
- Once production runs a version, its section is removed. The history stays in git and in [RELEASE-NOTES.md](RELEASE-NOTES.md).
- Database migrations apply on their own when the app starts.
