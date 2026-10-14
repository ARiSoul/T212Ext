# T212Ext — backlog

The roadmap by phase, then ideas and known limitations not yet planned. When an item ships, it leaves this file; what it became stays in [RELEASE-NOTES.md](RELEASE-NOTES.md) and in git history.

Done so far:
- **2.0.0, phase 1 (foundations):** API client, database, scheduled sync, CSV import, first pages, tests.
- **2.6.0:** installable app (PWA): manifest in the chosen language, icons, a service worker that caches nothing.
- **2.5.0:** automatic CSV reports: asked for through the API during the sync, imported like an uploaded file, listed on the import page.
- **2.4.0, phase 4 (deployment):** sign-in with password and authenticator code, Docker image and compose sharing only the reverse proxy and SQL Server with traceon (neutral `edge` and `data` networks, own login), data protection keys on a volume, own encrypted backups, CI check of the image, DEPLOYMENT and OPERATIONS docs.
- **2.3.0:** capital gains for Anexo J quadro 9.2A (FIFO, ECB rates, splits), quadro 11 account details.
- **2.2.0, phase 3 (IRS report):** Anexo J quadro 8A from dividends, ECB rates, source country per instrument, Portuguese dividends and interest apart, CSV export and print.
- **2.1.0, phase 2 (dividend dashboard):**
  - dividend charts and figures: monthly, yearly, by instrument, country and currency, yield on cost, forward estimate, passive income;
  - pages for positions, cash movements and orders;
  - grids with search, filters, sorting, totals, paging and CSV export;
  - the new look, with light, dark and automatic themes;
  - Portuguese, English and Spanish.

## Contents

- [IRS report follow-ups](#irs-report-follow-ups)
- [Deployment follow-ups](#deployment-follow-ups)
- [Phase 5 — Extras (2.6 and later)](#phase-5--extras-26-and-later)
- [App and grid follow-ups](#app-and-grid-follow-ups)
- [Known limitations and technical debt](#known-limitations-and-technical-debt)
- [Product ideas](#product-ideas)

## IRS report follow-ups

Phase 3 shipped as **2.2.0** (dividends) and **2.3.0** (capital gains): see [TAX.md](TAX.md). Left for later:

| # | Item | Notes |
|---|---|---|
| 3.6b | **Confirm the interest code and country** | E21 or E22, and the country of the Trading 212 entity. Once confirmed, remove the "to confirm" badge, or make it a setting per account. |
| 3.8b | **Treaty caps per country** | A table of treaty dividend rates (US 15%, CH 15%, FR 12.8%…) to show the capped credit next to the tax withheld, once it is clear which one to declare. |
| 3.9b | **CSV order import** | Orders from the CSV reports (IDs `EOF…`), matched to the API's by instrument, time and quantity, for accounts whose API history misses orders. The maintainer's account does not need it: the API has the same 1,469 filled orders as the reports. |
| 3.9c | **Corporate actions** | Spin-offs, mergers and scrip dividends in the FIFO (splits are done). |
| 3.9d | **Englobamento check** | An optional field for the year's other taxable income, to say whether the balance of short holdings must be aggregated. |
| 3.9e | **Inflation coefficient** | For holdings over 24 months (art. 50.º), if Anexo J's instructions confirm it applies. |
| 3.10 | **Domestic dividends in Anexo E** | Only if the maintainer chooses aggregation (englobamento). |

## Deployment follow-ups

Phase 4 shipped as **2.4.0**: see [DEPLOYMENT.md](DEPLOYMENT.md). Left for later:

| # | Item | Notes |
|---|---|---|
| 4.9 | **Content-Security-Policy** | The app already sends `X-Frame-Options`, `X-Content-Type-Options` and `Referrer-Policy`. A CSP needs care: the theme script in `App.razor` is inline, and Blazor and ApexCharts need their own allowances. |
| 4.10 | **Sign-in history** | A small table of recent sign-ins (time, address, success), shown on a page, instead of only the logs. |
| 4.11 | **Image from CI** | Build the image in GitHub Actions and pull it on the server (GHCR), instead of building on the server. Worth it only if the server's builds get slow. |
| 4.12 | **Infrastructure project** | Move Caddy, SQL Server and the shared networks out of traceon's compose into a project of their own, with both apps as equal guests. Worth it with a third app, or before touching traceon's SQL Server for another reason. Moving SQL Server means a short downtime and a backup and restore of every database. |

## Phase 5 — Extras (2.6 and later)

- **Notifications:** a Web Push when a dividend arrives, and a monthly summary. traceon already has VAPID and Web Push.
- **Upcoming dividends calendar** from an external provider (EODHD, Financial Modeling Prep, …). It also makes a real forward estimate possible. Check each provider's licence for personal use first.
- **Goals:** for example, "dividends cover X% of my monthly expenses", with progress over time.
- **Dividend growth per instrument:** the yearly growth of the dividend per share (from `GrossAmountPerShare`), and the cuts.
- **Sector of each holding** (external data), for a sector breakdown.

## App and grid follow-ups

Recorded 2026-10-13 with the 2.1 work.

- **Column chooser** per grid (show and hide columns), saved like the rest of the grid state.
- **Saved views:** named sets of filters and sort per grid.
- **Date range filter** on date columns (today: by year).
- **Translate the dividend types** shown from the API codes (~60 codes; today they read as words, e.g. "Dividends paid by us corporations").
- **Instrument page:** a link to the instrument's page at Trading 212.
- **UI tests with bUnit** for the grid component and the pages' empty and error states.
- **A theme preference per account** once there is sign-in (today, per browser).

## Known limitations and technical debt

- **Orders from CSV reports are not imported** (3.9b). Capital gains rely on the API's order history.
- **CSV action names not seen yet** land in "Other lines skipped". Examples: other order types, card payments, currency conversions, transfers between accounts. When one shows up, decide whether it matters, and update [T212-API.md](T212-API.md#csv-reports) and the parser together.
- **Transactions pagination** fails after page 2 for some accounts (a Trading 212 bug). The sync then logs a failed job; CSV reports fill the gap.
- **Users live in configuration:** adding one or changing a password means editing `.env` and recreating the container ([OPERATIONS.md](OPERATIONS.md#4-users-and-sign-in)). Fine for one or two people.
- **One instrument list for all accounts**, refreshed with the first configured account's key.
- **Daily snapshots exist only from the day the app first ran.** Earlier portfolio values cannot be rebuilt from the API. The portfolio chart fills in over time.
- **Summaries in several currencies:** grid totals add up whatever is in the column. That is fine while an account has one currency; with accounts in two currencies, filter by account first.

## Product ideas

Recorded 2026-10-13. Not planned; the focus is personal use.

- **Why not a product as is:** Trading 212's API Terms require written consent for apps used by other people ([T212-API.md](T212-API.md#terms-of-use)).
- **Paths, if it ever makes sense:**
  1. **Self-hosted open source.** Each person runs their own copy with their own key, which is personal use. Possible income from donations or sponsorships.
  2. **A CSV-based multi-broker product:** dividend tracking and tax reports from broker exports, with no dependence on any API. The data sources are already pluggable (API and CSV), so this is a matter of more parsers.
  3. **Ask Trading 212 for consent** once there is something worth showing.
- **The niche worth noting:** dividend tax reports for Portuguese residents (Anexo J), where foreign brokers give little help. 2.2 does exactly that.
