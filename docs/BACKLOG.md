# T212Ext — backlog

The roadmap by phase, then ideas and known limitations not yet planned. When an item ships, it leaves this file; what it became stays in [RELEASE-NOTES.md](RELEASE-NOTES.md) and in git history.

Done so far:
- **2.0.0, phase 1 (foundations):** API client, database, scheduled sync, CSV import, first pages, tests.
- **2.1.0, phase 2 (dividend dashboard):**
  - dividend charts and figures: monthly, yearly, by instrument, country and currency, yield on cost, forward estimate, passive income;
  - pages for positions, cash movements and orders;
  - grids with search, filters, sorting, totals, paging and CSV export;
  - the new look, with light, dark and automatic themes;
  - Portuguese, English and Spanish.

## Contents

- [Phase 3 — IRS report (2.2)](#phase-3--irs-report-22)
- [Phase 4 — Deploy next to traceon (2.3)](#phase-4--deploy-next-to-traceon-23)
- [Phase 5 — Extras (2.4 and later)](#phase-5--extras-24-and-later)
- [App and grid follow-ups](#app-and-grid-follow-ups)
- [Known limitations and technical debt](#known-limitations-and-technical-debt)
- [Product ideas](#product-ideas)

## Phase 3 — IRS report (2.2)

Branch `feature-irs-report`. **The maintainer's priority after the dashboard** (2026-10-13). It comes before the deploy because it is useful even locally, once a year.

**Goal:** for a tax year, the figures a Portuguese tax resident needs for the IRS return, and nothing to calculate by hand. Foreign dividends go in **Anexo J, quadro 8A** (rendimentos de capitais; the dividend code has been E11, to confirm each year), with the tax withheld abroad for the double-taxation credit.

| # | Item | Notes |
|---|---|---|
| 3.1 | **ECB exchange rates** | Daily reference rates (free) for every currency an amount was in. Store them, and fetch the missing days. Which date's rate applies (payment date, or the last business day before it) is set by the Código do IRS (art. 23.º): confirm the wording before building it. |
| 3.2 | **Gross amounts in euros** | Per dividend: gross = shares × gross per share (instrument currency) → EUR at the payment date's rate. Withholding tax (CSV) → EUR the same way. Cross-check: gross − withheld ≈ net received. Flag the dividends that differ by more than a few cents. |
| 3.3 | **Source country per dividend** | The ISIN country by default. It is wrong for some instruments: Irish-domiciled ETFs holding US shares are Irish income, but ADRs pay from the company's country. Store a per-instrument override, editable on the instrument page. |
| 3.4 | **Dividend adjustments** | US withholding refunds (`DIVIDEND_ADJUSTMENT`) reduce the tax withheld of the year in their note ("2024 US dividends withholding tax adjustment"). Store the note on cash movements (a migration) and parse the year. |
| 3.5 | **Report page** | Year selector. Per country: number of payments, gross, tax withheld, net, all in EUR. The lines in the order quadro 8A asks: código, país da fonte, rendimento bruto, imposto pago no estrangeiro. Totals. A drill-down to the dividends of each line, as a grid. |
| 3.6 | **Interest and other income** | Interest on cash and share lending paid by Trading 212: list them apart from dividends, with the paying entity's country (check which Trading 212 entity holds the account), for the maintainer to decide where they go. Do not guess the quadro. |
| 3.7 | **Export** | CSV (and a print-friendly page, or PDF) with the lines and the per-dividend detail behind them, to keep with the return. |
| 3.8 | **Disclaimer and checks** | The page says what it computed and how. It warns about dividends without withholding data (imported only from the API) and about missing exchange rates. Before relying on it, check each year's form and the instructions of the Portal das Finanças. **Not tax advice.** |
| 3.9 | **Optional: sales (Anexo J, quadro 9.2A)** | Capital gains from sales also go in Anexo J. That needs FIFO cost per lot, from orders (API) plus CSV order import (see below). It is larger, so decide once 3.1–3.8 are done. |

**Effort:** 18–25 h for 3.1–3.8, and 15–20 h more for 3.9.

## Phase 4 — Deploy next to traceon (2.3)

Branch `feature-deploy`. Run T212Ext on the Hetzner VPS that already runs traceon.

| # | Item | Notes |
|---|---|---|
| 4.1 | **Sign-in** | Required before exposure. Options: ASP.NET Core Identity with one user and two-step verification (preferred), or Caddy `basic_auth` as a stop-gap. |
| 4.2 | **Dockerfile** | Multi-stage, non-root, `curl` for the health check at `/health`. |
| 4.3 | **Compose and network** | Its own `docker-compose.yml` on a shared external Docker network (e.g. `edge`) that traceon's Caddy joins too. One more Caddyfile site, e.g. `dividends.arisoul.org { reverse_proxy t212ext:8080 }`. |
| 4.4 | **Database** | A new `T212Ext` database on traceon's SQL Server Express, with its own login (not `sa`). No extra RAM, and traceon's backups can include it. Check that the backup scripts in traceon's `ops/backup` cover a second database. |
| 4.5 | **Secrets** | Keys and secrets in the server's `.env`. Trading 212 keys restricted to the VPS IP address. |
| 4.6 | **Data protection keys** | Persist the ASP.NET Core data protection keys to a volume, so sign-in and language cookies survive restarts. |
| 4.7 | **Docs** | `docs/DEPLOYMENT.md`, `docs/OPERATIONS.md`, the first `UPGRADING.md` section. |
| 4.8 | **PWA** | Installable on the phone, as traceon. |

**Effort:** 12–18 h.

## Phase 5 — Extras (2.4 and later)

- **Notifications:** a Web Push when a dividend arrives, and a monthly summary. traceon already has VAPID and Web Push.
- **Automatic CSV reports:** request a report through `/history/exports` every month, download it and import it. This adds withholding tax and closes the API's history gaps without manual work.
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
- **Instrument page:** a link to the instrument's page at Trading 212, and the source-country override (3.3).
- **UI tests with bUnit** for the grid component and the pages' empty and error states.
- **A theme preference per account** once there is sign-in (today, per browser).

## Known limitations and technical debt

- **Orders from CSV reports are not imported.** Report order IDs (`EOF…`) are not the API's order ids, so matching needs rules of their own (ticker, time, quantity). Needed for 3.9 and to cover the API's missing order history.
- **CSV action names not seen yet** land in "Other lines skipped". Examples: other order types, card payments, currency conversions, transfers between accounts. When one shows up, decide whether it matters, and update [T212-API.md](T212-API.md#csv-reports) and the parser together.
- **Transactions pagination** fails after page 2 for some accounts (a Trading 212 bug). The sync then logs a failed job; CSV reports fill the gap.
- **No sign-in** until phase 4. Do not expose the app.
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
- **The niche worth noting:** dividend tax reports for Portuguese residents (Anexo J), where foreign brokers give little help. Phase 3 is exactly that.
