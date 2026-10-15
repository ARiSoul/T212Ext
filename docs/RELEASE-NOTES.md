# T212Ext release notes

What changed in each version, for the person using the app. Versions follow [semantic versioning](https://semver.org); the branch prefix decides the bump (see [README.md](README.md#versions)). Each released version is a git tag (`v2.0.0`) with a GitHub release.

## 2.7.1 — Unknown commands are refused (2026-10-15)

**Fixed**
- A command the app does not know (for example one a newer version adds, run on the old image) is refused with the list of the known ones, instead of starting a second copy of the app.

## 2.7.0 — Notifications (2026-10-15)

**New**
- **Notifications** on the phone or the computer:
  - when a dividend arrives (after the sync that finds it; several at once in one notification);
  - early each month, the dividends of the month before, compared with the same month a year earlier;
  - in the language the app had on that device; a tap opens the dividends.
- **Notifications page** (menu): turn them on or off on this device, send a test, and see and remove the devices that get them. On the iPhone they work in the installed app (2.6.0).
- Turning them on never sends the history: only what arrives from then on.
- New settings `Push:*` (the server's VAPID keys); without them notifications stay off.

## 2.6.0 — Install it on the phone (2026-10-15)

**New**
- **Installable app.** On the phone, "Add to Home Screen" (iPhone: Share → Add to Home Screen; Android: the browser's menu → Install app); on the computer, the install button in the address bar. It opens in its own window with its own icon, and long-pressing the icon on Android offers shortcuts to Dividends and the IRS report.
- The name, description and shortcuts follow the app's language.
- Nothing is kept offline: every page comes from the server, so figures are never stale and a new version shows at once.

## 2.5.0 — Automatic CSV reports (2026-10-14)

No more exporting reports by hand.

**New**
- **Automatic CSV reports.** During the sync, the app asks Trading 212 for a CSV report of the latest days (at most once a week), downloads it and imports it, as if you had uploaded it:
  - the tax withheld on each dividend, which only the reports have, so the IRS report stays complete;
  - what the API's history misses;
  - the first time, it goes back to the account's first activity, one year per report, over one or a few syncs.
- **The Import CSV page lists them:** period, what each one added, and whether it is imported, waiting for Trading 212, or failed (with the reason). It also says how far the history is covered.
- Settings `Reports:*`, on by default (see the README).

## 2.4.1 — Pages load on the server (2026-10-14)

**Fixed**
- **Blank page after signing in on the server.** The Docker image left out Blazor's script, so no page could start. The image now includes it, and CI checks that it loads.
- **A refused Trading 212 key** (401) now also says to check the key's allowed IP addresses: a key restricted to another address is refused the same way as a wrong one.

**Changed**
- If the connection to the server is lost for good, a bar at the bottom says so, with a link to reload, instead of a page that no longer responds.

## 2.4.0 — Ready for the server (2026-10-13)

Phase 4: T212Ext can run on the Hetzner server it shares with traceon, reachable from anywhere over HTTPS and protected by a sign-in. See [DEPLOYMENT.md](DEPLOYMENT.md).

**New**
- **Sign-in.** Every page needs it once users are configured:
  - a password, stored only as a hash, and an optional (recommended) code from an authenticator app;
  - a session lasts 14 days from the last visit; changing the password ends every session;
  - after 5 wrong attempts in a minute, the page asks to wait;
  - a sign-out button at the top right.
- **Running on the server:**
  - a Docker image and a compose file; the two apps stay independent and share only the reverse proxy and SQL Server, through two neutral networks;
  - its own database and login on the shared SQL Server, created by a script;
  - its own nightly encrypted backups, off the server, with a weekly restore test;
  - [DEPLOYMENT.md](DEPLOYMENT.md) for the first setup and [OPERATIONS.md](OPERATIONS.md) for updates, users, secrets and restores.

**Changed**
- Locally nothing changes: without users configured, the app stays open in Development. Anywhere else it refuses to start without users.
- The app now tells browsers not to show it inside other sites, and not to send its addresses to other sites.
- Security updates: .NET libraries 10.0.13, and a fixed `System.Security.Cryptography.Pkcs` (two denial-of-service advisories of 2026-10-13).

## 2.3.0 — Capital gains for the IRS (2026-10-13)

The IRS report gets a second tab: the sales of shares and ETFs, for Anexo J, quadro 9.2A. See [TAX.md](TAX.md#capital-gains-anexo-j-quadro-92a). To check, not tax advice.

**New**
- **Capital gains (quadro 9.2A).** Every sale of the year is matched to the shares bought longest ago (FIFO), one line per purchase:
  - sale and purchase dates and values in euros, at the ECB rate of each date;
  - expenses (fees on the purchase and the sale);
  - code G01 for shares, G20 for ETFs (to confirm);
  - source country, counterparty country, "Sim" for listed securities;
  - lines numbered from 951, with CSV export and print.
- **Totals:**
  - sale value, purchase value, expenses and the year's balance, next to Trading 212's own result (average cost);
  - the balance of what was held less than 365 days, with when it must be aggregated;
  - the partial exclusion for holdings of 2, 5 or 8 years.
- **Stock splits:** Trading 212 records no order for a split, so you can enter them (date and new shares per share). When sales exceed purchases, the page names the instrument and the date, which is usually a split to enter.
- **Quadro 11:** both tax tabs show your account number and the country of the Trading 212 entity, for identifying the account abroad.

**Changed**
- Exchange rates are kept from the first trade as well as from the first dividend.
- The setting for the Trading 212 entity's country is shared by both tabs.

## 2.2.0 — IRS report (2026-10-13)

Phase 3: the figures for the Portuguese tax return, prepared from your dividends. To check, not tax advice. See [TAX.md](TAX.md).

**New**
- **IRS report** (menu "IRS report"), for any year:
  - **Anexo J, quadro 8A:** code E11 and source country (Tabela X numeric code), with gross and tax paid abroad in euros, one line per country, numbered from 801;
  - **Portuguese dividends** listed apart, with the 35% withheld in Portugal: they are not foreign income;
  - **interest and share lending**, with a suggested code (E21) and the country of your Trading 212 entity, both to confirm;
  - **withholding refunds** for the year (US "Dividend adjustment"), deducted from the tax paid unless you switch it off;
  - **every dividend** with the exchange rate used and a check column (estimated or derived amounts, no rate, return of capital, no country, withholding above 15%);
  - export to CSV, and a print-friendly page.
- **ECB exchange rates:** stored and refreshed after each sync, or with "Update exchange rates". The rate is the payment date's, or the last quote before it.
- **Source country per instrument:** on each instrument's page, for when the ISIN's country is not where the income comes from (ADRs, for example).

**Changed**
- CSV import keeps the report's net per share. With the API's gross per share, the tax withheld is now exact, where before it was rounded to cents.
- Cash movements keep the report's note (e.g. "2024 US dividends withholding tax adjustment").

**Fixed**
- Rows imported only from CSV reports took the report's net per share as the gross. The gross now adds the withholding back.

**After updating:** import your CSV reports once more. Nothing is duplicated, and it fills the net per share the exact withholding needs.

## 2.1.0 — Dividend dashboard (2026-10-13)

Phase 2: see how your dividend income grows, in a new look, in three languages.

**New**
- **Overview:**
  - the last 12 months against the 12 before;
  - this year against the same period last year;
  - an estimate for the next 12 months;
  - yield on cost, all-time total and portfolio value;
  - charts: dividends per month (with the 12-month average), passive income (dividends, interest, share lending), top payers and the portfolio over time.
- **Dividends:**
  - dividends per year with the growth of each year;
  - one year against the one before, month by month;
  - breakdowns by country and by currency.
- **Income by instrument:** what each instrument has paid, all time and in the last 12 months, with its share of the total. Each instrument has a page with its payments and its dividend per share over time.
- **Positions:** the latest snapshot, with the dividends of the last 12 months, yield on cost, current yield and the estimate for the next 12 months.
- **Cash movements** and **Orders** pages.
- **Every list is a grid** with search, filters (year, country, type, account…), sorting, totals (sum, average, min, max, count over all filtered rows), paging and CSV export. The grids remember your choices in the browser.
- **New look**, with a light theme, a dark theme, or the system's.
- **Languages:** Portuguese (default), English and Spanish, from the menu at the top.
- With several accounts, an account selector at the top filters every page.

**Fixed**
- Importing reports one after another could take two days of share lending with the same amount for one movement, and drop one. Each movement now remembers the CSV row it came from, and only movements no CSV row has claimed are matched by value, within one hour.

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
- No sign-in yet: do not expose the app to the internet (planned for the deployment phase).
- Orders are not imported from CSV reports yet.
