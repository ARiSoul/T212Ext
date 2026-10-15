# T212Ext

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor)](https://learn.microsoft.com/aspnet/core/blazor/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Status: Alpha](https://img.shields.io/badge/Status-Alpha-orange)]()

**T212Ext** keeps a complete, permanent copy of your [Trading 212](https://www.trading212.com) dividends, orders and cash movements, and helps you see how your dividend income grows.

Trading 212's own app shows dividends one by one. T212Ext reads them through the [public API](https://docs.trading212.com) on a schedule, adds what only the CSV reports have (the tax withheld on each dividend), and keeps everything in your own database: totals by month and year, the last 12 months, and daily snapshots of the portfolio that the API itself does not keep.

> **Personal use.** Trading 212's [API Terms](https://www.trading212.com/legal-documentation/API-Terms_EN.pdf) allow the API for your own use. Apps used by other people need Trading 212's written consent. See [docs/T212-API.md](docs/T212-API.md#terms-of-use).

---

## Features (2.13)

**IRS report (Portugal)**
- **Anexo J, quadro 8A:** foreign dividends by source country (code E11), gross and tax paid abroad, in euros at the ECB rate of each payment date. Exact withholding from the API's gross and the CSV's net per share.
- **Apart:** Portuguese dividends (35% withheld in Portugal) and interest/share lending (code to confirm).
- **Withholding refunds** deducted from the year they refer to.
- **Capital gains, quadro 9.2A:** every sale matched to its purchases by FIFO, with values in euros at the ECB rate of each date, expenses, and codes G01 or G20. Also: the year's balance, the balance of holdings under 365 days, the partial exclusion for long holdings, and the stock splits you enter.
- **Quadro 11:** your account number, for identifying the account abroad.
- **Checks** line by line, CSV export, and a print-friendly page. See [docs/TAX.md](docs/TAX.md). *Figures to check, not tax advice.*


**Dividends, at a glance**
- **Overview:**
  - the last 12 months against the 12 before, and this year against the same period last year;
  - an estimate for the next 12 months, yield on cost, the all-time total and the portfolio value;
  - charts: dividends per month with the 12-month average, passive income (dividends, interest, share lending), top payers, and the portfolio over time.
- **Dividends:** totals per year with their growth, one year against the one before month by month, and breakdowns by country and currency.
- **Income by instrument:** what each instrument has paid, all time and in the last 12 months, and how its dividend per share grew. Each instrument has its own page with its payments and its dividend per share by year (growth, years of growth in a row, cuts).
- **Goals:** a passive income target a month, or a share of your expenses, with the progress over time and, at the current pace, when you would reach it.
- **Positions:** the latest snapshot, with dividends, yield on cost, current yield and the 12-month estimate per position.
- **Cash movements and orders:** deposits, withdrawals, interest, share lending, withholding tax refunds, and buys and sells with fees and realised gains.

**Every list is a grid** with:
- search, filters (year, country, type, account…) and sorting;
- totals (sum, average, min, max, count) over all filtered rows;
- paging and CSV export.

The grids remember your choices.

**Data, kept complete**
- **Scheduled sync** of every configured account (Invest and Stocks ISA) every 6 hours. It reads the summary and positions (one snapshot per day), and the dividend, order and cash movement history.
- **Incremental and resumable.** Each sync stops at the first page with nothing new, and an interrupted first sync is completed by the next one. It respects Trading 212's rate limits.
- **CSV import** of Trading 212 reports, several at once. It adds the **withholding tax** per dividend, which the API does not give, and fills gaps in the API's history. Rows seen in both sources are merged, never duplicated.
- **Automatic reports:** the sync asks Trading 212 for these CSV reports itself, every week, and imports them. The first time it goes back to the account's first activity. Manual uploads are needed only for what they do not cover.

**Look and language**
- A modern interface with **light, dark and automatic themes**, made for phone and desktop.
- **Português, English, Español.**

**Hosting**
- **Sign-in** with a password and an authenticator code (TOTP), for running on a server.
- **Installable** on the phone and the computer ("Add to Home Screen", "Install app"): its own icon and window, no browser bars.
- **Notifications** when a dividend arrives, and a monthly summary compared with the year before, in each device's language (Web Push; on the iPhone in the installed app).
- **Docker** image and compose file for the server it shares with traceon: independent apps that share only the reverse proxy (HTTPS) and SQL Server, through neutral networks. Own database login, own encrypted backups. See [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

Next: see [docs/BACKLOG.md](docs/BACKLOG.md).

## Tech stack

| Layer | Technology |
|---|---|
| **App** | ASP.NET Core 10, Blazor Web App (Interactive Server), Bootstrap 5, Bootstrap Icons, Blazor-ApexCharts |
| **Sync** | `BackgroundService` in the same process, typed `HttpClient` |
| **Database** | SQL Server (LocalDB in development, Express in production), EF Core 10 with migrations |
| **Hosting** | Docker Compose behind the server's shared Caddy (HTTPS) and SQL Server; cookie sign-in with TOTP |
| **Tests** | xUnit; integration tests against a throwaway SQL Server database |

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server: **LocalDB** (comes with Visual Studio) or any SQL Server you can reach.
- The EF Core CLI, only to create migrations: `dotnet tool install --global dotnet-ef`.

### 1. Create a Trading 212 API key

In the Trading 212 app: **Settings → API (Beta) → Generate API key**. See Trading 212's [help article](https://helpcentre.trading212.com/hc/en-us/articles/14584770928157-Trading-212-API-key).

- **Permissions:** tick only the read permissions: *Account data*, *Portfolio*, *Metadata* and the three *History* ones (dividends, orders, transactions). T212Ext never places orders. A key without order permissions cannot trade, even if it leaks.
- **IP restriction:** on a server, limit the key to the server's IP address.
- **The secret is shown once.** Copy it straight into the configuration below, never into a file in the repository.
- **Older keys have no secret.** Keys made before about October 2025 are a single key. They still work, so leave `ApiSecret` empty. Trading 212 may stop accepting them one day; the sync log will then show a 401, and the fix is a new key pair.

Each account (Invest, Stocks ISA) needs its own key.

### 2. Configure

Accounts live under `T212:Accounts`. `appsettings.Development.json` already defines one account, `invest`. Put its key and secret in **user secrets**, which are stored outside the repository:

```bash
cd src/T212Ext.Web
dotnet user-secrets set "T212:Accounts:0:ApiKey" "<your API key>"
dotnet user-secrets set "T212:Accounts:0:ApiSecret" "<your API secret>"   # skip for an older key without a secret
```

A second account (e.g. a Stocks ISA) is another list entry:

```bash
dotnet user-secrets set "T212:Accounts:1:Key" "isa"
dotnet user-secrets set "T212:Accounts:1:Name" "Stocks ISA"
dotnet user-secrets set "T212:Accounts:1:ApiKey" "<key>"
dotnet user-secrets set "T212:Accounts:1:ApiSecret" "<secret>"
```

| Setting | Default | Meaning |
|---|---|---|
| `ConnectionStrings:T212Ext` | LocalDB in development | SQL Server database. Created and migrated at startup. |
| `T212:Accounts:N:Key` | | Stable id stored in the database: lowercase letters, digits and dashes. Never change it once data exists. |
| `T212:Accounts:N:Name` | | Name shown in the app. |
| `T212:Accounts:N:Environment` | `Live` | `Live` or `Demo` (practice account). A key works only in its own environment. |
| `T212:Accounts:N:ApiKey` | empty | Without it the account is not synced, but CSV import still works. |
| `T212:Accounts:N:ApiSecret` | empty | The secret of a key pair. Empty for an older key without a secret. |
| `Sync:Enabled` | `true` | Scheduled sync on or off. "Sync now" always works. |
| `Sync:Interval` | `06:00:00` | Time between scheduled syncs. |
| `Sync:RunOnStartup` | `true` | Sync once when the app starts. |
| `Sync:InstrumentsMaxAge` | `1.00:00:00` | How old the instrument list may get before it is refreshed. |
| `Reports:Enabled` | `true` | Ask for CSV reports during the sync and import them. |
| `Reports:Interval` | `7.00:00:00` | A new report is asked for once the covered history is older than this. |
| `Reports:Overlap` | `3.00:00:00` | Each report starts this long before the last one ended. |
| `Reports:WaitForReport` | `00:03:00` | How long a sync waits for a report; one not ready is collected by the next sync. |
| `Reports:HistoryStart` | empty | Where the first report starts for an account with no data yet; default one year back. |
| `Ui:DefaultCulture` | `pt-PT` | Language until one is chosen in the app (`pt-PT`, `en-GB` or `es-ES`). |
| `Auth:Users:N:Username` | | Who may sign in. Without users the app is open, which only Development allows. |
| `Auth:Users:N:PasswordHash` | | Hash made with `hash-password` (below). |
| `Auth:Users:N:TotpSecret` | empty | Authenticator secret made with `new-totp-secret`. Empty: password only. |
| `DataProtection:KeysPath` | empty | Folder for the cookie encryption keys. Set by the Docker image. |
| `Push:PublicKey`, `Push:PrivateKey` | empty | VAPID keys for notifications, made with `new-vapid-keys`. Empty: notifications off. |
| `Push:Subject` | empty | A contact for the push services, `mailto:<address>`. |
| `Logos:Enabled` | `true` | Fetch instrument logos (by the server, once each). Off: coloured avatars only. |

On a server, the settings come from environment variables instead (`T212__Accounts__0__ApiKey=…`): see [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

**Sign-in.** Locally the app is open while `Auth:Users` is empty. To try the sign-in, add a user to the user secrets; the two commands print what to store and touch nothing else:

```bash
dotnet run --project src/T212Ext.Web -- hash-password
```

```bash
dotnet run --project src/T212Ext.Web -- new-totp-secret <username>
```

```bash
dotnet user-secrets set "Auth:Users:0:Username" "<username>" --project src/T212Ext.Web
```

Then `Auth:Users:0:PasswordHash` and, optionally, `Auth:Users:0:TotpSecret` the same way.

### 3. Run

```bash
dotnet run --project src/T212Ext.Web
```

Open http://localhost:5067. The first start creates the database, and a first sync runs straight away.

> **Exposing it to the internet:** follow [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md). Outside Development the app refuses to start without users.

**If startup fails with "LocalDB … SQL Server process failed to start"**, the LocalDB instance is running in a context your process cannot reach. This happens, for example, when a sandboxed tool or another session started it. Stop it, and let the app start it again:

```bash
sqllocaldb stop MSSQLLocalDB
```

### 4. Import your history (recommended)

The API's history is incomplete for some accounts, and it never includes withholding tax. The sync fixes both on its own by asking Trading 212 for CSV reports (`Reports:*` above); the **Import CSV** page lists them.

To do it by hand instead, or for a period they do not cover: in the Trading 212 app, go to **History → Export**. Pick the whole period (one year per report at most) and include dividends, interest and transactions. Upload each file on the **Import CSV** page.

## Tests

```bash
dotnet test T212Ext.slnx
```

- `tests/T212Ext.Client.Tests`: the API client against a stubbed HTTP handler. Covers authentication, pagination, rate limits, retries and errors.
- `tests/T212Ext.Core.Tests`: sync, CSV parsing and import, merging between sources, analytics and queries, against a real SQL Server.
  - Each run creates a throwaway database and drops it at the end.
  - The server is LocalDB by default. Set `T212EXT_TEST_SQL` to use another one (a connection string without `Database`).
- `tests/T212Ext.Web.Tests`: the grid engine (filters, date ranges, sorting, totals, paging, export, columns, saved views) and the grid component (bUnit), sign-in (passwords, authenticator codes, return URLs, settings), languages, and that the three translations have the same keys.

## Project layout

```
T212Ext.slnx
VERSION                       Version of the whole solution
src/
  T212Ext.Client/             Trading 212 API client: models, auth, rate limits, paging
  T212Ext.Core/               Entities, EF Core (Data/), sync (Sync/), CSV import (Import/), automatic reports (Reports/), notifications (Notifications/), logos (Logos/), analytics (Analytics/),
                              ECB rates (Rates/), IRS report (Tax/), queries (Queries/)
  T212Ext.Web/                Blazor app; hosts the sync worker
    Auth/                     Sign-in: users from configuration, authenticator codes, setup commands
    Components/Grid/          The DataGrid and its engine
    Components/Charts/        Chart style (validated palette) and the themed chart card
    Resources/                Texts in English, Portuguese and Spanish
tests/
  T212Ext.Client.Tests/
  T212Ext.Core.Tests/
  T212Ext.Web.Tests/
ops/                          Server scripts: database setup (db/), backups (backup/), Caddy site (caddy/)
Dockerfile, docker-compose.yml, .env.example   The server deployment (docs/DEPLOYMENT.md)
docs/                         See docs/README.md
```

## Documentation

| File | What it is |
|---|---|
| [docs/README.md](docs/README.md) | Index, versioning and releasing |
| [docs/CONVENTIONS.md](docs/CONVENTIONS.md) | Rules every change follows |
| [docs/TAX.md](docs/TAX.md) | The IRS report: rules, sources, what is still to confirm |
| [docs/T212-API.md](docs/T212-API.md) | What we know about the Trading 212 API: auth, limits, quirks, terms |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | First-time setup on the server it shares with traceon |
| [docs/OPERATIONS.md](docs/OPERATIONS.md) | Updates, logs, users, secrets, backups on the server |
| [docs/BACKLOG.md](docs/BACKLOG.md) | Roadmap and ideas |
| [docs/RELEASE-NOTES.md](docs/RELEASE-NOTES.md) | What changed in each version |

## History

Versions 1.x were a .NET MAUI app (2024–2025). It talked to the API from the phone, kept data on the device, and only synced while a page was open. Version 2.0 is a rewrite as a server app, so the history is collected even when nobody is looking. The 1.x code is in the git history before the 2.0 tag.

## License

[MIT](LICENSE)
