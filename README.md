# T212Ext

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor)](https://learn.microsoft.com/aspnet/core/blazor/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Status: Alpha](https://img.shields.io/badge/Status-Alpha-orange)]()

**T212Ext** keeps a complete, permanent copy of your [Trading 212](https://www.trading212.com) dividends, orders and cash movements, and helps you see how your dividend income grows.

Trading 212's own app shows dividends one by one. T212Ext reads them through the [public API](https://docs.trading212.com) on a schedule, adds what only the CSV reports have (the tax withheld on each dividend), and keeps everything in your own database: totals by month and year, the last 12 months, and daily snapshots of the portfolio that the API itself does not keep.

> **Personal use.** Trading 212's [API Terms](https://www.trading212.com/legal-documentation/API-Terms_EN.pdf) allow the API for your own use. Apps used by other people need Trading 212's written consent. See [docs/T212-API.md](docs/T212-API.md#terms-of-use).

---

## Features (2.0)

- **Scheduled sync** of every configured account (Invest and Stocks ISA) every 6 hours:
  - account summary and open positions, kept as one **snapshot per day**;
  - **dividend**, **order** (with fills, FX rate and fees) and **cash movement** history.
- **Incremental and resumable.** Each sync reads newest first and stops at the first page with nothing new. An interrupted first sync is completed by the next one.
- **Rate limits handled.** It waits for Trading 212's limits from the response headers, and retries network errors and 5xx responses.
- **CSV import** of Trading 212 reports:
  - adds the **withholding tax** per dividend, which the API does not give;
  - fills gaps the API leaves (its history pagination is known to skip data).
  
  Rows seen in both sources are merged, not duplicated. Importing the same file twice changes nothing.
- **Dividends page:**
  - totals by year, and by month for a chosen year;
  - every payment with shares, gross per share, tax withheld and net amount.
- **Overview:**
  - dividends for the last 12 months, this year and all time;
  - per account: total value, invested, unrealised P/L and free cash.
- **Sync log** with each job's result. "Full history" reads everything again.

The roadmap (dividend charts, yield on cost, an IRS Anexo J report, deployment, notifications…) is in [docs/BACKLOG.md](docs/BACKLOG.md).

## Tech stack

| Layer | Technology |
|---|---|
| **App** | ASP.NET Core 10, Blazor Web App (Interactive Server), Bootstrap 5 |
| **Sync** | `BackgroundService` in the same process, typed `HttpClient` |
| **Database** | SQL Server (LocalDB in development, Express in production), EF Core 10 with migrations |
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
| `Ui:Culture` | `pt-PT` | Number and date format. |

On a server, use environment variables instead of user secrets (`T212__Accounts__0__ApiKey=…`).

### 3. Run

```bash
dotnet run --project src/T212Ext.Web
```

Open http://localhost:5067. The first start creates the database, and a first sync runs straight away.

> **Do not expose the app to the internet yet.** It has no sign-in until version 2.2 (see the [backlog](docs/BACKLOG.md#phase-3--deploy-next-to-traceon-22)).

**If startup fails with "LocalDB … SQL Server process failed to start"**, the LocalDB instance is running in a context your process cannot reach. This happens, for example, when a sandboxed tool or another session started it. Stop it, and let the app start it again:

```bash
sqllocaldb stop MSSQLLocalDB
```

### 4. Import your history (recommended)

The API's history is incomplete for some accounts, and it never includes withholding tax. In the Trading 212 app, go to **History → Export**. Pick the whole period (one year per report at most) and include dividends, interest and transactions. Upload each file on the **Import CSV** page.

## Tests

```bash
dotnet test T212Ext.slnx
```

- `tests/T212Ext.Client.Tests`: the API client against a stubbed HTTP handler. Covers authentication, pagination, rate limits, retries and errors.
- `tests/T212Ext.Core.Tests`: sync, CSV parsing and import, merging between sources, and queries, against a real SQL Server.
  - Each run creates a throwaway database and drops it at the end.
  - The server is LocalDB by default. Set `T212EXT_TEST_SQL` to use another one (a connection string without `Database`).

## Project layout

```
T212Ext.slnx
VERSION                       Version of the whole solution
src/
  T212Ext.Client/             Trading 212 API client: models, auth, rate limits, paging
  T212Ext.Core/               Entities, EF Core (Data/), sync (Sync/), CSV import (Import/), queries (Queries/)
  T212Ext.Web/                Blazor app; hosts the sync worker
tests/
  T212Ext.Client.Tests/
  T212Ext.Core.Tests/
docs/                         See docs/README.md
```

## Documentation

| File | What it is |
|---|---|
| [docs/README.md](docs/README.md) | Index, versioning and releasing |
| [docs/CONVENTIONS.md](docs/CONVENTIONS.md) | Rules every change follows |
| [docs/T212-API.md](docs/T212-API.md) | What we know about the Trading 212 API: auth, limits, quirks, terms |
| [docs/BACKLOG.md](docs/BACKLOG.md) | Roadmap and ideas |
| [docs/RELEASE-NOTES.md](docs/RELEASE-NOTES.md) | What changed in each version |

## History

Versions 1.x were a .NET MAUI app (2024–2025). It talked to the API from the phone, kept data on the device, and only synced while a page was open. Version 2.0 is a rewrite as a server app, so the history is collected even when nobody is looking. The 1.x code is in the git history before the 2.0 tag.

## License

[MIT](LICENSE)
