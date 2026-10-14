# Upgrading

Deploy steps for the versions that are not yet in production.

The usual deploy command and checks are in [OPERATIONS.md](OPERATIONS.md#1-deploying-an-update).

**Production runs 2.5.0** (since 2026-10-14).

## 2.6.0

- Only the usual deploy: no migration, no new setting.
- **Try afterwards** on the phone: open the site, sign in, and add it to the home screen (iPhone: Share → Add to Home Screen; Android: the browser's menu → Install app). It must open in its own window, with the green icon, and keep the session. On Android, long-press the icon: the shortcuts are Dividends and IRS report.

## 2.7.0

- **Migration** `AddPushNotifications`: two new tables, applied when the app starts.
- **New settings (optional):** `Push__PublicKey`, `Push__PrivateKey`, `Push__Subject` in `.env`, as in [DEPLOYMENT.md](DEPLOYMENT.md#11-notifications-optional). Without them notifications stay off and the page says so.
- **Try afterwards:** on the phone, in the installed app, **Notifications** → **Turn on here** → **Send a test**. The next sync after a dividend should announce it.

## How this file works

- Each version section lists only what is extra to the usual deploy command: migrations to be aware of, new settings, and what to try afterwards.
- Versions are deployed in order, oldest first.
- Once production runs a version, its section is removed. The history stays in git and in [RELEASE-NOTES.md](RELEASE-NOTES.md).
- The "Production runs" line above changes in that same edit.
- Database migrations apply on their own when the app starts.
