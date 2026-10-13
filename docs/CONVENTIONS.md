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
4. **Secrets live only in user secrets (development) or the server's environment.**
   - This covers API keys and secrets, and passwords in connection strings.
   - They are never pasted into chats, issues, `appsettings*.json` or the repository.
5. **Tests:**
   - The client is tested against `StubHandler`. Everything that touches the database is tested against the throwaway SQL Server of `DatabaseFixture`.
   - Every fix and every new rule gets a test that fails without it.
   - The whole suite passes before a PR is opened.
   - Tests never call Trading 212.
6. **Merge only with CI green** and the branch up to date with `main`. Docs-only PRs can merge at once.
7. **Language:** code, comments, docs, commits and PRs are in English. The maintainer is addressed in European Portuguese.

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

## 4. App (`src/T212Ext.Web`)

1. **Mobile first.** Every page works at 375 px wide and on desktop: Bootstrap breakpoints, `flex-wrap`, `table-responsive`, columns hidden with `d-none d-md-table-cell` rather than squeezed. Both widths are checked before the PR.
2. **Pages read through the query classes** (`Core/Queries`), never through `DbContext` directly. Queries use `AsNoTracking`.
3. **UI text is English for now; numbers and dates follow `Ui:Culture`** (`pt-PT` by default). Localisation is in the backlog.
4. **Long work runs in the worker, not in a page.** A page asks `SyncCoordinator.Request` and listens to `StateChanged`.
