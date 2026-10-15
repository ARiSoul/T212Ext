# T212Ext — backlog

The roadmap by phase, then ideas and known limitations not yet planned. When an item ships, it leaves this file; what it became stays in [RELEASE-NOTES.md](RELEASE-NOTES.md) and in git history.

Done so far:
- **2.0.0, phase 1 (foundations):** API client, database, scheduled sync, CSV import, first pages, tests.
- **2.15.0:** the calendar by month (paid and expected, an agenda on phones), the holdings with nothing expected and why; late payments judged slot by slot.
- **2.14.0:** projected dividend calendar: last year's payments a year later, times today's shares; due and late payments apart; the forward estimate is its sum.
- **2.13.0:** passive income goals: an amount a month or a share of the expenses, progress on the last 12 months, the pace of the last year and when it would get there.
- **2.12.0:** dividend growth per instrument: per share, by year, compared only between years with the same number of payments; streak, compound growth, cuts.
- **2.11.0:** dividend types translated (the API's 29 types and their manufactured payments); a link to the instrument's page at Trading 212 (US and London).
- **2.10.0:** instrument icons: logos (Trading 212's, then by ISIN) fetched once by the server, or a coloured avatar with the code.
- **2.9.0:** grids: column chooser, saved views, date range filter; bUnit tests of the grid.
- **2.8.0:** IRS follow-ups: treaty limits per country (credit capped by default), interest and share-lending codes, the mandatory aggregation check, a note on the inflation coefficient.
- **2.7.0:** notifications (Web Push): new dividends and a monthly summary, in each device's language; a page to turn them on per device.
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
- [Phase 5 — Extras (2.16 and later)](#phase-5--extras-216-and-later)
- [App and grid follow-ups](#app-and-grid-follow-ups)
- [Known limitations and technical debt](#known-limitations-and-technical-debt)
- [Product ideas](#product-ideas)

## IRS report follow-ups

Phase 3 shipped as **2.2.0** (dividends) and **2.3.0** (capital gains): see [TAX.md](TAX.md). Left for later:

| # | Item | Notes |
|---|---|---|
| 3.6c | **Interest split by entity** | When an account moves from Trading 212 Markets Ltd (Cyprus) to Trading 212 EU GmbH (Germany), split the year's interest by date between the two countries. Needs the move date (not announced). |
| 3.9b | **CSV order import** | Orders from the CSV reports (IDs `EOF…`), matched to the API's by instrument, time and quantity, for accounts whose API history misses orders. The maintainer's account does not need it: the API has the same 1,469 filled orders as the reports. |
| 3.9c | **Corporate actions** | Spin-offs, mergers and scrip dividends in the FIFO (splits are done). |
| 3.9e | **Inflation coefficient** | Compute it for shares held over 24 months (art. 50.º), if the AT confirms that it applies to Anexo J (today a note; the AT applies it in the assessment). Needs the yearly coefficients (Portaria). |
| 3.10 | **Domestic dividends in Anexo E** | Only if the maintainer chooses aggregation (englobamento). |

## Deployment follow-ups

Phase 4 shipped as **2.4.0**: see [DEPLOYMENT.md](DEPLOYMENT.md). Left for later:

| # | Item | Notes |
|---|---|---|
| 4.9 | **Content-Security-Policy** | The app already sends `X-Frame-Options`, `X-Content-Type-Options` and `Referrer-Policy`. A CSP needs care: the theme script in `App.razor` is inline, and Blazor and ApexCharts need their own allowances. |
| 4.10 | **Sign-in history** | A small table of recent sign-ins (time, address, success), shown on a page, instead of only the logs. |
| 4.11 | **Image from CI** | Build the image in GitHub Actions and pull it on the server (GHCR), instead of building on the server. Worth it only if the server's builds get slow. |
| 4.12 | **Infrastructure project** | Move Caddy, SQL Server and the shared networks out of traceon's compose into a project of their own, with both apps as equal guests. Worth it with a third app, or before touching traceon's SQL Server for another reason. Moving SQL Server means a short downtime and a backup and restore of every database. |

## Phase 5 — Extras (2.16 and later)

- **Announced dividends on top of the projected calendar:** EODHD's free key (20 calls a day) returns declared future dividends (`/api/div`, amount and currency; pay dates often missing outside North America). Ask only for holdings with a projected payment in the next ~45 days, and replace the projection when one is announced. First check, with a free key, that it serves Euronext, Xetra and LSE symbols; if it only serves the US, the paid "Historian" plan is the next step. Its terms allow storing the data for private use only.
- **Sector of each holding**, for a sector breakdown, from free sources: SEC EDGAR's SIC code (US issuers and foreign ones registered there), else Wikidata's industry by ISIN (mapped to ~11 sectors; coverage is uneven), else entered by hand. Distributing ETFs by hand, or Alpha Vantage's free ETF profile if it covers UCITS.

## App and grid follow-ups

Recorded 2026-10-13 with the 2.1 work.

- **Trading 212 links for other exchanges:** the instrument page links to Trading 212 only for US and London tickers; the addresses for Xetra (`d`), Paris (`p`), Amsterdam (`a`) and the rest are not confirmed.
- **UI tests with bUnit** for the pages' empty and error states (the grid component has them since 2.9.0).
- **A theme preference per account** once there is sign-in (today, per browser).
- **Notification choices:** per device, which notifications to get (dividends, monthly summary), and quiet hours.

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
