# T212Ext release notes

What changed in each version, for the person using the app. Versions follow [semantic versioning](https://semver.org); the branch prefix decides the bump (see [README.md](README.md#versions)). Each released version is a git tag (`v2.0.0`) with a GitHub release.

## 2.0.0 — The rewrite (2026-10-13)

T212Ext is now a web app that runs on a server and collects your Trading 212 data on its own, instead of a phone app that only synced while a page was open.

**New**
- **Scheduled sync** every 6 hours (and on demand with **Sync now**) of each configured account:
  - account summary and positions, kept as a daily snapshot;
  - dividend, order and cash movement history.
- **Incremental and resumable:** each sync reads newest first and stops at the first page with nothing new. An interrupted first sync is completed by the next one.
- **Trading 212's rate limits are respected.** The app waits when it is told to, and retries network errors.
- **CSV import** of Trading 212 reports:
  - adds the tax withheld on each dividend, which the API does not give;
  - fills gaps in the API's history.

  The same dividend from both sources is kept once. Importing the same file twice changes nothing.
- **Overview:**
  - dividends for the last 12 months, this year and all time;
  - each account's value, invested amount, unrealised P/L and free cash.
- **Dividends:**
  - totals by year, or by month for a chosen year;
  - each payment with shares, gross per share, tax withheld and net amount.
- **Sync log** with each job's result, and **Full history** to read everything again.
- Several accounts (Invest and Stocks ISA), each with its own API key.
- CSV import reads the reports Trading 212 exports today ("Time (UTC)"), several files at once. Withholding tax refunds ("Dividend adjustment") are kept as cash movements.

**Changed**
- Uses Trading 212's current API: the new `account/summary`, `positions` and `{order, fill}` history.
- Works with both kinds of API key: a key pair (key and secret), and an older key without a secret.
- Data lives in SQL Server, not on the phone.

**Removed**
- The .NET MAUI app (1.x). Its code stays in git history.

**Known limitations**
- No sign-in yet: do not expose the app to the internet (planned for 2.2).
- Orders are not imported from CSV reports yet.
