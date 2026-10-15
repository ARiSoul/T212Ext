# T212Ext release notes

What changed in each version, for the person using the app. Versions follow [semantic versioning](https://semver.org); the branch prefix decides the bump (see [README.md](README.md#versions)). Each released version is a git tag (`v2.0.0`) with a GitHub release.

## 2.17.0 — Content-Security-Policy (2026-10-15)

**Security**
- Every page now tells the browser to run scripts only from this site, or the page's own inline scripts with a code that changes on every request (a nonce). Text that slipped into a page could not run a script, nor load anything from another site.
- The page can no longer be shown inside another site's frame (as before), nor send forms or connect anywhere but this site.
- Nothing changes in use: the theme, the charts, the notifications and the installable app work as before.

## 2.16.0 — Sign-in history (2026-10-15)

**New**
- **Sign-ins** (new page, under Data): every attempt to sign in over the last 90 days, with when, the user, the result (signed in, wrong password or code, unknown user), the address and the device.
- On top: the failures in the last 24 hours and 30 days, the sign-ins and from how many addresses, and your previous sign-in, to spot one that was not you.
- An unknown username is recorded as such, not as typed (it may be a password typed in the wrong field). Attempts are kept 90 days.

## 2.15.0 — Calendar by month (2026-10-15)

**New**
- **A month view** on the calendar page: each day with its payments (logo and amount), the paid ones and the expected ones in different colours, and the month's totals. Back and forward a year. On a phone it is an agenda of the days with payments.
- **Holdings with nothing expected** are listed under the calendar, with the reason: no dividend yet (or an accumulating fund), none for over 13 months, or last year's payments not repeated.

**Fixed**
- Holdings that pay an interim and a final dividend (uneven gaps between payments, common in Europe) were taken as late a few months after their last payment, and every later payment dropped out of the calendar and of the estimate for the next 12 months. A payment is now late only when last year's one was not repeated in time, and the later ones are still expected.

## 2.14.0 — Dividend calendar (2026-10-15)

**New**
- **Calendar** (new page): the dividends your holdings are expected to pay in the next 12 months, by date and by month, with what is due in the next 30 days and the next payment.
- Projected from what each holding paid: every payment of the last 12 months again a year later, times the shares you hold today. Interim and final dividends keep their own amounts; holdings that pay three or more times a year use the latest amount, so raises show at once.
- Payments whose date has just passed show as "any day now", and those well past it as "late" (a cut or a suspension shows up here).
- Special dividends, bonuses and capital gains distributions are left out. Holdings with under a year of history are filled in at their usual pace.

**Changed**
- **The estimate for the next 12 months** (overview, positions, an instrument's page, goals) is now the calendar's sum. It leaves out special dividends and holdings that stopped paying, and follows the latest raise of regular payers.

## 2.13.0 — Goals (2026-10-15)

**New**
- **Goals** (new page): set a passive income target, either an amount a month or a share of your monthly expenses ("dividends pay half the electricity bill"), and choose whether interest and share lending count.
- Each goal shows how far you are (the last 12 months' income a month against the target), the next 12 months' estimate, how much it grew in a year, and, at that pace, when it would be reached.
- A chart and a table show the income a month over time against the target.

## 2.12.0 — Dividend growth (2026-10-15)

**New**
- **Each instrument's page** has "Dividend per share, by year": the dividend per share of every year, how many payments, and the change against the year before. On top: the growth last year (marked when it was a cut), how many years in a row it grew, and the average yearly growth.
- **Income by instrument** shows the growth last year (red for a cut) and the years of growth, to sort and spot the cuts.
- Measured per share in the instrument's currency, so buying more shares or the exchange rate do not move it. Two years are compared only with the same number of payments, so a payment that slips into January does not look like a cut.

## 2.11.0 — Dividend types in your language (2026-10-15)

**New**
- **Dividend types are translated** on the dividends page and each instrument's page: "Dividendos pagos por empresas dos EUA" instead of "Dividends paid by us corporations", the same name whether the dividend came from the API or a CSV report. A payment received instead of a dividend while the shares were lent shows its type with "(compensação, ações emprestadas)".
- **"Open at Trading 212"** on an instrument's page, for US and London instruments.

## 2.10.0 — Instrument icons (2026-10-15)

**New**
- **Every instrument shows its icon** in dividends, income by instrument, positions, orders and both IRS tabs, and large on each instrument's page:
  - the logo Trading 212's app shows, or, when it has none, one found by ISIN;
  - otherwise a coloured avatar with the ticker's code, always the same colour for the same ticker.
- The server fetches each logo once and keeps it: your browser never contacts those sites. `Logos__Enabled=false` keeps it to avatars.

## 2.9.0 — Grids: columns, views and dates (2026-10-15)

**New**, in every list:
- **Columns:** show and hide columns ("Columns" button); hidden columns are not searched or exported.
- **Saved views:** give a name to the current filters, dates, sort, totals and columns ("Views" button), and come back to them with one click. Up to 20 per list, kept in this browser.
- **Date range:** dividends, cash movements and orders can be filtered from one day to another, next to the filter by year. The totals follow.

## 2.8.0 — IRS: treaty limits and aggregation (2026-10-15)

See [TAX.md](TAX.md). To check, not tax advice.

**New**
- **Treaty limits per country** (the AT's table of treaties): each dividend is checked against its own country's treaty (10% for the Netherlands, Denmark, Japan and, from 2026, the United Kingdom; 15% for most; 28% for countries without a treaty, such as Sweden and Finland). The quadro 8A lines show the tax withheld, the treaty, and the tax to declare, capped at the treaty by default (a switch declares the full amount).
- **Mandatory aggregation check** on the capital gains tab: with the year's other taxable income (optional, kept only in the browser), it says whether the balance of holdings under 365 days must be aggregated.
- **A note on the inflation coefficient** for shares held more than 24 months.

**Changed**
- Interest on cash is code E21, now confirmed; share lending shows "E21 or E22" (no official source picks one; both are taxed alike).

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
