# T212Ext conventions

The rules every change follows, for people and for coding assistants. Each rule says why it exists and where it lives in the code.

- **When a PR sets a new rule, it adds that rule here in the same PR.**
- **A rule that turns out wrong is changed here first**, then in the code.

## 1. Workflow

1. **One branch and one PR per piece of work, from `main`, named after the version bump it brings.**
   - Use `major-<name>`, `feature-<name>` or `hotfix-<name>` (see [README.md](README.md#versions)).
   - Each code PR is a release: it bumps `VERSION` and adds its section to `docs/RELEASE-NOTES.md`.
2. **Stage files by name.** Never use `git add .` or `git add -A`, and check `git status --short` before committing.
   - `.claude/` (local launch settings) stays untracked.
   - *Why:* folder-wide adds pick up secrets and local files.
3. **Each PR that changes the database model carries its migration.**
   - Create it with `dotnet ef migrations add <Name> --project src/T212Ext.Core --startup-project src/T212Ext.Web --output-dir Data/Migrations`.
   - Review its `Up`/`Down`.
   - The app applies migrations when it starts. Nobody applies them by hand.
4. **Secrets live only in user secrets (development) or the server's `.env` (production).**
   - This covers API keys and secrets, and passwords in connection strings.
   - They are never pasted into chats, issues, `appsettings*.json` or the repository.
   - Passwords are stored only as hashes (`hash-password`); `.env.example` holds names, never values.
5. **Tests:**
   - The client is tested against `StubHandler`. Everything that touches the database is tested against the throwaway SQL Server of `DatabaseFixture`.
   - Every fix and every new rule gets a test that fails without it.
   - The whole suite passes before a PR is opened.
   - Tests never call Trading 212.
   - Pure logic (analytics, grid engine) is tested without a database; `tests/T212Ext.Web.Tests` covers the web project's logic and the translations.
   - A sandboxed tool (a coding assistant) may not reach the LocalDB instance another process started, and `dotnet ef` commands start `MSSQLLocalDB` in the tool's own context: stop it afterwards (`sqllocaldb stop MSSQLLocalDB`) so the maintainer's app can start it. It then uses its own instance through `T212EXT_TEST_SQL`, e.g. `Server=(localdb)\T212ExtClaude;...`.
6. **Merge only with CI green** and the branch up to date with `main`. Docs-only PRs can merge at once.
7. **Language:** code, comments, docs, commits and PRs are in English. The maintainer is addressed in European Portuguese.
8. **A package with a known vulnerability fails the build** (NuGet audit warnings are errors).
   - Update the package that brings it. If no fixed version of that package exists yet, pin the vulnerable one at a fixed version in the project that brings it, with a comment naming the advisory, and remove the pin once the parent brings a fixed version.
   - *Why:* the app is reachable from the internet. Advisories appear without any change on our side, so CI can turn red overnight (2.4.0: `System.Security.Cryptography.Pkcs` through `Microsoft.Data.SqlClient`).

## 2. Trading 212 API client (`src/T212Ext.Client`)

1. **Enums from the API are strings, never C# enums.**
   - *Why:* Trading 212 adds values without notice; the dividend `type` list alone has ~60. An unknown value must not fail a sync.
   - Every model property is nullable, since the API marks nothing as required.
2. **Every call goes through `T212ApiClient.SendWithRetriesAsync`.**
   - It waits on the per-account `T212RateLimiter`, retries 429s (up to 5 waits) and transient failures (408, 5xx, network; 3 retries with backoff), and turns everything else into `T212ApiException`.
   - Don't add another retry layer on top.
3. **Rate limits are per account, so there is one limiter per account** (`T212ApiFactory`).
   - The limiter learns from the `x-ratelimit-*` headers.
   - `T212Endpoint.Period` is only the fallback for 429s that come without headers.
4. **Pagination uses only the query string of `nextPagePath`**, applied to the endpoint's own path, and stops with an error on a cursor it has seen before.
   - *Why:* users report `nextPagePath` values in other route formats, and cursors that repeat.
5. **Credentials are sent only to `live.trading212.com` / `demo.trading212.com`.**
   - Report download links point to another host and are fetched without the `Authorization` header.
   - `T212Account.ToString()` never prints the key or the secret.
   - Key pairs use HTTP Basic. An older key without a secret is sent bare (`T212Account.AuthorizationHeader`). Both stay supported until Trading 212 retires the older keys.
6. **No order placement.** The client only reads. Writing endpoints are left out on purpose; the recommended key has no order permissions.

## 3. Data and sync (`src/T212Ext.Core`)

1. **Every imported row has a natural unique key, and every import is idempotent.**
   - Keys:
     - dividends and cash movements: `(AccountId, Reference)`;
     - orders: `(AccountId, OrderId, FillId)`, checked in code;
     - snapshots: `(AccountId, Date)`;
     - instruments: `Ticker`.
   - Running a sync or an import twice changes nothing; there is a test for each source.
2. **History jobs save page by page and stop early only once the history is complete** (`SyncState.HistoryComplete`).
   - *Why:* a first sync interrupted halfway would otherwise stop at the first known page forever, leaving the older history missing.
3. **A row seen in the API and in a CSV report is one row, with `Sources = Api | Csv`.**
   - CSV rows usually have no reference. They are matched on instrument (ISIN), date (±3 days), quantity and net amount (`DividendMatcher`), or on type, amount and date (±1 day) for cash (`CashTransactionMatcher`).
   - The API's reference replaces the synthetic `csv:` one.
   - The CSV's withholding tax is kept.
4. **Cash movement signs are normalised:** money in is positive, money out (withdrawals, fees) negative (`CashTransactionTypes.NormaliseSign`). Transfers keep their sign.
5. **Amounts in different currencies are never added together.** Summaries are per currency (`DividendSummary.Currency`).
6. **Decimals are `decimal(28,10)` everywhere** (`T212ExtDbContext.ConfigureConventions`). Fractional shares and FX rates need the digits.
7. **Credentials are never stored in the database.** `BrokerAccount` holds only the configuration key, name, environment and what the API says about the account.
8. **Text read from files is parsed with `CultureInfo.InvariantCulture`**, and synthetic references use invariant formatting.
   - *Why:* the server runs with `pt-PT`, where `0.48` would be read as 48.
9. **A stored row absorbs one CSV row at most, and remembers it (`CsvReference`).** Matching by value only considers rows no CSV row has claimed.
   - *Why:* share lending pays every day, often the same amount, and two days were once taken for one. *(2.1.0)*

10. **Tax figures follow [TAX.md](TAX.md), and each rule there names its source.**
    - The calculation lives in `Core/Tax/IrsReport.cs`, a pure function tested in `IrsReportTests`. A rule changes in TAX.md and in the code in the same PR.
    - Anything not confirmed by an official source shows as "to confirm" on the page, never as fact.
    - Money for the tax report is converted per payment with `RateTable` (the ECB rate of the date, else the last quote before it) and rounded to cents per payment.

## 4. App (`src/T212Ext.Web`)

1. **Every text a person reads is translated.**
   - It goes through `IStringLocalizer<SharedResource>` (`L["Key"]`), with the texts in `Resources/SharedResource.resx` (English), `.pt.resx` (European Portuguese) and `.es.resx` (Spanish).
   - This includes `title`, `aria-*` and placeholders. Only logs are exempt.
   - Every key exists in the three files with the same `{0}` placeholders; `LocalizationTests` checks it, and that every `L["Key"]` used exists.
   - Codes from Trading 212 that have no translation go through `Labels.Humanize`, so new codes still read well.
   - The language is a cookie set by `/language`; the page reloads to apply it. `/language` only redirects to local URLs.
2. **Every list is a `DataGrid`** (`Components/Grid`), with search, filters, sorting, totals, paging and CSV export.
   - Columns give a raw `Value` (for sorting and totals) and the `Text` shown.
   - Filters go on columns with few distinct values (year, type, country, account).
   - Totals cover every filtered row, not only the page.
   - The filter, sort and totals logic lives in `GridEngine`, tested in `GridEngineTests`; the component only renders it.
   - Each grid has its own `StorageKey`; its state is saved in the browser.
3. **Colours come from the tokens in `wwwroot/app.css`** (`--app-*`), defined for light and dark. No hard-coded colours in components.
   - The accent is the app's green, not Bootstrap's blue.
   - Positive and negative amounts use `text-positive` and `text-negative`.
4. **Charts use `ChartStyle` and `ThemedChart`.**
   - Series colours come from the validated palette, in its fixed order (`ChartStyle.Series(slot)`). A series keeps its colour; it is never picked by rank.
   - One y-axis per chart: two measures of different units are two charts.
   - Every chart has its numbers in a tooltip and in a grid or table on the same page (some light colours are under 3:1 against white).
   - `ThemedChart` waits for the theme and rebuilds the chart when it changes.
5. **Mobile first.** Every page works at 375 px wide and on desktop.
   - Grids switch to cards below `md`; table columns are hidden with `HideBelow`, not squeezed.
   - No page scrolls sideways; wide tables scroll inside their card.
   - Both widths are checked before the PR.
6. **Pages read through the query classes** (`Core/Queries`), never through `DbContext` directly. Queries use `AsNoTracking`. Pages that depend on the chosen account inherit `AccountPage`.
7. **No prerendering** (`App.razor`): pages load their data once, and JS interop (theme, saved grid state) works from the first render.
8. **Long work runs in the worker, not in a page.** A page asks `SyncCoordinator.Request` and listens to `StateChanged`.
9. **Every endpoint needs a signed-in user unless it says otherwise** (the fallback policy in `Auth/AuthSetup.cs`).
   - The open ones are marked `AllowAnonymous`: `/login`, `/auth/login`, `/health`, `/language` and the static files. A new open endpoint is a decision, written here.
   - Users come from configuration (`Auth:Users`), never from the database. Outside Development the app refuses to start without users.
   - Sign-in failures do not say which part was wrong, sign-in is rate-limited per address, and an authenticator code works once.
   - *Why:* the app holds a whole portfolio and tax history, and is reachable from the internet.
10. **A page that sets or clears a cookie is a plain form**, not interactive: it is marked `[ExcludeFromInteractiveRouting]` and posts to an endpoint with `<AntiforgeryToken />` (`Login.razor`, the sign-out button). Every other page is interactive.
11. **Redirects go only to local paths** (`SignInService.SafeReturnUrl`): the sign-in and language links cannot send anyone to another site.

## 5. Deployment (`Dockerfile`, `docker-compose.yml`, `ops/`)

1. **The server runs what the repository holds.** Its settings are only in `.env`; tracked files are never edited there, so `git pull` always applies cleanly.
2. **A new setting the server needs goes, in the same PR, to `.env.example`, the settings table in the README, and the version's section in `UPGRADING.md`.**
3. **T212Ext depends on the server's shared infrastructure, never on another app.**
   - It joins only the external networks `edge` (the reverse proxy) and `data` (SQL Server as `sqlserver`), never another project's network, and publishes no port.
   - Names on those networks are DNS names: every service and container is `t212ext-<role>` (today `t212ext-web`), never generic (`web`, `api`) nor `sqlserver`, `caddy` or `traceon-*`.
   - Its site is a file in the proxy's sites folder, with the domain written in it; the docs always validate the configuration before reloading Caddy. Its backups, scripts and secrets are its own. Nothing in this repository calls another app's scripts.
   - The infrastructure's side of the contract is traceon's `docs/OPERATIONS.md`, section 20; a change there is checked against this section.
   - Where the infrastructure lives (today traceon's compose project) is a setting with a default (`SA_ENV_FILE`, `DB_CONTAINER`), not something the app is built around.
   - The app trusts forwarded headers only because nothing but the proxy can reach it.
   - *Why:* the two apps are independent; a change in one must not break the other, and either could move to another server.
4. **Database work outside migrations is a script in `ops/db/`, safe to run twice.**
   - The app's login owns only its own database, and the app and its nightly backup use only that login.
   - `sa` is used only to create the database and login, and to restore (restore test, disaster restore).
5. **CI builds the image and starts it** against a SQL Server: it must refuse to start without users, answer `/health` with one, and serve the Blazor script the sign-in page references (fingerprinted, as JavaScript). A change to the `Dockerfile` or the compose file is checked there before it reaches the server.
   - *Why:* the Dockerfile restores from the project files alone, before any `.razor` file is there, and the SDK then leaves out `blazor.web.js` unless `RequiresAspNetWebAssets` is set in the csproj. The 2.4.0 image had no Blazor script: every page stayed blank after sign-in.
6. **Server docs are commands to copy, one per block**, with `<placeholders>` for personal values ([DEPLOYMENT.md](DEPLOYMENT.md), [OPERATIONS.md](OPERATIONS.md)). They never show a secret's value.
