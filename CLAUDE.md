# T212Ext

Read [docs/CONVENTIONS.md](docs/CONVENTIONS.md) before changing code, and follow it. When a change sets a new lasting rule, add the rule there in the same PR. What we know about the Trading 212 API (and its quirks) is in [docs/T212-API.md](docs/T212-API.md); update it when you learn something new.

## Layout

- `src/T212Ext.Client/`: Trading 212 API client (no database, no ASP.NET). `IT212Api`, `T212ApiClient`, rate limiting, models.
- `src/T212Ext.Core/`: everything else that is not UI.
  - `Domain/`: entities and matching rules;
  - `Data/`: `T212ExtDbContext`, configurations, migrations;
  - `Accounts/`: configured accounts;
  - `Sync/`: synchronizer, runner, worker;
  - `Import/`: CSV parser and import;
  - `Reports/`: CSV reports asked for through the API during the sync (`ReportImporter`);
  - `Notifications/`: Web Push devices and what is announced after the sync (`PushDevices`, `Notifier`);
  - `Analytics/`: dividend figures, computed in memory from plain facts;
  - `Rates/`: ECB exchange rates (source, updater, `RateTable`);
  - `Tax/`: the IRS report (`IrsReport`; rules in docs/TAX.md);
  - `Queries/`: read models for the UI.
- `src/T212Ext.Web/`: Blazor Web App (Interactive Server). It hosts the sync worker and applies migrations at startup.
  - `Auth/`: sign-in (users from configuration, TOTP, `hash-password` and `new-totp-secret` commands);
  - `Components/Grid/`: `DataGrid` and `GridEngine`;
  - `Components/Charts/`: `ChartStyle` (validated palette) and `ThemedChart`;
  - `Components/Shared/`: `AccountPage`, `Fmt`, `Labels`, selectors;
  - `Resources/`: `SharedResource` texts (en, pt, es);
  - `wwwroot/app.css`: the design tokens (light and dark).
- `tests/T212Ext.Client.Tests/`: client tests with a stubbed HTTP handler.
- `tests/T212Ext.Core.Tests/`: integration tests against a throwaway SQL Server database (LocalDB by default, `T212EXT_TEST_SQL` to override), and analytics tests.
- `tests/T212Ext.Web.Tests/`: grid engine, languages and translation tests.
- `Dockerfile`, `docker-compose.yml`, `.env.example`, `ops/`: the server deployment next to traceon (docs/DEPLOYMENT.md, docs/OPERATIONS.md).
- `docs/`: see [docs/README.md](docs/README.md).

## Commands

```bash
dotnet build T212Ext.slnx
dotnet test T212Ext.slnx
dotnet run --project src/T212Ext.Web
dotnet ef migrations add <Name> --project src/T212Ext.Core --startup-project src/T212Ext.Web --output-dir Data/Migrations
```

## Working with the maintainer

- Talk to the maintainer in European Portuguese.
- Code, docs, commits and PRs are in English.
- Branches are named after the version bump they bring: `major-<name>`, `feature-<name>` or `hotfix-<name>` (docs/CONVENTIONS.md, section 1).
- Never put secrets (API keys and secrets, connection strings with passwords) in the chat or the repository.
- Never act on production, and never call the live Trading 212 API from tests.
