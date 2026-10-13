# T212Ext — backlog

The roadmap by phase, then ideas and known limitations not yet planned. When an item ships, it leaves this file; what it became stays in [RELEASE-NOTES.md](RELEASE-NOTES.md) and in git history.

Phase 1 (foundations) shipped as **2.0.0**: API client, database, scheduled sync, CSV import, first pages, tests.

## Contents

- [Phase 2 — Dividend dashboard (2.1)](#phase-2--dividend-dashboard-21)
- [Phase 3 — Deploy next to traceon (2.2)](#phase-3--deploy-next-to-traceon-22)
- [Phase 4 — Extras (2.3 and later)](#phase-4--extras-23-and-later)
- [Known limitations and technical debt](#known-limitations-and-technical-debt)
- [Product ideas](#product-ideas)

## Phase 2 — Dividend dashboard (2.1)

Branch `feature-dividend-dashboard`. The goal: understand the dividend income at a glance and watch it grow. Charts with Blazor-ApexCharts, as in traceon.

| # | Item | Notes |
|---|---|---|
| 2.1 | **Monthly income chart** | Bars per month, a 12-month moving average, and the same month a year before. |
| 2.2 | **Year over year** | Total per year, growth %, and a cumulative line. |
| 2.3 | **By instrument** | Ranking of payers (all time, last 12 months), each instrument's share of the income, and payment history per instrument (a detail page). |
| 2.4 | **By country and currency** | Country from the ISIN prefix; currency from `InstrumentCurrency`. |
| 2.5 | **Yield on cost** | Dividends of the last 12 months divided by `TotalCost` of the latest position snapshot, per position and overall. |
| 2.6 | **Forward income estimate** | Each holding's last 12 months of dividends per share × the shares held today. A naive estimate, labelled as such. |
| 2.7 | **Passive income** | Dividends + interest on cash + lending interest (cash movements), per month. |
| 2.8 | **Portfolio over time** | From the daily snapshots: value, cost and unrealised P/L. |
| 2.9 | **Positions page** | Latest snapshot with each position's dividends and yield on cost. |

**Effort:** 20–30 h.

## Phase 3 — Deploy next to traceon (2.2)

Branch `feature-deploy`. Run T212Ext on the Hetzner VPS that already runs traceon.

| # | Item | Notes |
|---|---|---|
| 3.1 | **Sign-in** | Required before exposure. Options: ASP.NET Core Identity with one user and two-step verification (preferred), or Caddy `basic_auth` as a stop-gap. |
| 3.2 | **Dockerfile** | Multi-stage, non-root, `curl` for the health check at `/health`. |
| 3.3 | **Compose and network** | Its own `docker-compose.yml` on a shared external Docker network (e.g. `edge`) that traceon's Caddy joins too. One more Caddyfile site, e.g. `dividends.arisoul.org { reverse_proxy t212ext:8080 }`. |
| 3.4 | **Database** | A new `T212Ext` database on traceon's SQL Server Express, with its own login (not `sa`). No extra RAM, and traceon's backups can include it. Check the backup scripts in traceon's `ops/backup` cover a second database. |
| 3.5 | **Secrets** | Keys and secrets in the server's `.env`. Trading 212 keys restricted to the VPS IP address. |
| 3.6 | **Data protection keys** | Persist the ASP.NET Core data protection keys to a volume, so sign-in cookies survive restarts. |
| 3.7 | **Docs** | `docs/DEPLOYMENT.md`, `docs/OPERATIONS.md`, the first `UPGRADING.md` section. |
| 3.8 | **CI** | A GitHub Actions build-and-test on PRs, with a SQL Server service, as in traceon (the workflow exists from 2.0; check it runs once the repository has PRs). |

**Effort:** 10–15 h.

## Phase 4 — Extras (2.3 and later)

- **IRS report (Anexo J, quadro 8A).** For each year: foreign dividends by source country, gross amount, and tax withheld, in euros. Needs:
  - withholding tax from CSV reports (already stored);
  - gross amounts in euros, using the ECB reference rate of the payment date (fetch and store ECB rates);
  - the source country from the ISIN, with a way to correct it per instrument (ADRs, Irish-domiciled ETFs);
  - export to CSV or PDF, in the order the form expects.

  This is probably the most useful feature for a Portuguese tax resident. Check each year's form before relying on it. **Effort:** 15–20 h.
- **Notifications:** a Web Push when a dividend arrives, and a monthly summary. traceon already has VAPID and Web Push.
- **Automatic CSV reports:** request a report through `/history/exports` every month, download it and import it. This closes the API's history gaps without manual work.
- **Upcoming dividends calendar** from an external provider (EODHD, Financial Modeling Prep, …). Check each provider's licence for personal use first; Trading 212's terms forbid redistributing its market data, not other providers'.
- **Goals:** for example, "dividends cover X% of my monthly expenses", with progress over time.
- **Localisation:** English, European Portuguese and Spanish, as in traceon.
- **PWA:** installable on the phone, as traceon.

## Known limitations and technical debt

- **Orders from CSV reports are not imported.** Report order IDs (`EOF…`) are not the API's order ids, so matching them needs rules of their own (ticker, time, quantity). Until then, orders come only from the API, which is known to miss history for some accounts.
- **CSV action names not seen yet** (other order types, card payments, currency conversions, transfers between accounts) land in "Other lines skipped". When one shows up, decide whether it matters, and update [T212-API.md](T212-API.md#csv-reports) and the parser together.
- **Dividend adjustments are not tied to a year.** The year they correct is only in the note ("2024 US dividends withholding tax adjustment"), which is not stored. The IRS report (phase 4) needs it: store the note, or parse the year from it.
- **Transactions pagination** fails after page 2 for some accounts (a Trading 212 bug). The sync then logs a failed job; CSV reports fill the gap.
- **No sign-in** until phase 3. Do not expose the app.
- **One instrument list for all accounts**, refreshed with the first configured account's key.
- **Daily snapshots exist only from the day the app first ran.** Earlier portfolio values cannot be rebuilt from the API.
- **UI tests:** none yet. Add bUnit tests when pages get logic of their own (phase 2).

## Product ideas

Recorded 2026-10-13. Not planned; the focus is personal use.

- **Why not a product as is:** Trading 212's API Terms require written consent for apps used by other people ([T212-API.md](T212-API.md#terms-of-use)).
- **Paths, if it ever makes sense:**
  1. **Self-hosted open source.** Each person runs their own copy with their own key, which is personal use. Possible income from donations or sponsorships.
  2. **A CSV-based multi-broker product:** dividend tracking and tax reports from broker exports, with no dependence on any API. The data sources are already pluggable (API and CSV), so this is a matter of more parsers.
  3. **Ask Trading 212 for consent** once there is something worth showing.
- **The niche worth noting:** dividend tax reports for Portuguese residents (Anexo J), where foreign brokers give little help.
