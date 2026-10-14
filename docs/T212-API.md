# The Trading 212 API

What we know about the [Trading 212 public API](https://docs.trading212.com), as checked on 2026-10-13 against the official OpenAPI spec (`https://docs.trading212.com/_spec/api.json`), the API Terms, the Help Centre and the official forum thread. Add to this file whenever a sync teaches us something new.

## Version and hosts

- **v0, marked "Beta".** There is no v1. In October 2025 Trading 212 announced a "major API upgrade" for the coming months; nothing has shipped under a new version yet.
- Live: `https://live.trading212.com/api/v0`. Demo (practice money): `https://demo.trading212.com/api/v0`.
- A key works only in the environment it was created in.
- Only **Invest** and **Stocks ISA** accounts. Not CFD and not SIPP. Each account needs its own key.
- **No multi-currency:** values come in the account's primary currency.

## Authentication

- **HTTP Basic:** the API key is the user name and the API secret the password (`Authorization: Basic base64(key:secret)`). It has been in place since around October 2025, when live orders arrived.
- **Older keys have no secret.** Keys made before key pairs existed are sent bare in the `Authorization` header. The spec still lists this as `legacyApiKeyHeader`, with no announced end date.
  - T212Ext uses it when `ApiSecret` is empty, and logs a warning on each sync.
  - A 401 on such a key suggests making a new key pair.
  - *To check:* not yet tested against a real older key; confirm on the first real sync.
- Keys have **permission scopes**. A missing scope answers 403 with e.g. `Scope( history:dividends ) missing for API key`.

  | Scope | Used for |
  |---|---|
  | `account` | `GET /equity/account/summary` |
  | `portfolio` | `GET /equity/positions` |
  | `metadata` | `GET /equity/metadata/instruments` |
  | `history:dividends` | `GET /equity/history/dividends` |
  | `history:orders` | `GET /equity/history/orders` |
  | `history:transactions` | `GET /equity/history/transactions` |
- Keys can be restricted to IP addresses. Use this on the server.
  - **A request from an address the key does not allow answers 401**, the same as a wrong key or secret, not 403. Seen on the first deploy (2026-10-14): a typo in the allowed address. The 401 message names this cause too.
  - The key's address is the server's IPv4 one (`curl -4 -s https://ifconfig.me`); containers go out through it.
- The secret is shown only once, when the key is created.

## Endpoints we use

| Endpoint | Rate limit | Notes |
|---|---|---|
| `GET /api/v0/equity/account/summary` | 1 / 5 s | `id`, `currency`, `totalValue`, `cash{availableToTrade, inPies, reservedForOrders}`, `investments{currentValue, totalCost, realizedProfitLoss, unrealizedProfitLoss}` |
| `GET /api/v0/equity/positions` | 1 / 1 s | `instrument{ticker,isin,name,currency}`, `quantity`, `averagePricePaid` and `currentPrice` (instrument currency), `walletImpact{currency,currentValue,totalCost,unrealizedProfitLoss,fxImpact}` |
| `GET /api/v0/equity/metadata/instruments` | 1 / 50 s | ~all tradable instruments; refreshed by Trading 212 every 10 min |
| `GET /api/v0/equity/history/dividends` | 20 / 1 min | Paged. `amount` is **net, in the account currency**; `grossAmountPerShare` in `tickerCurrency`; `type` has ~60 values (ORDINARY, RETURN_OF_CAPITAL, …) |
| `GET /api/v0/equity/history/orders` | 20 / 1 min | Paged. Each item is `{order, fill}`; `fill.walletImpact` has `fxRate`, `netValue`, `realisedProfitLoss` and `taxes[]` |
| `GET /api/v0/equity/history/transactions` | 20 / 1 min | Paged. Types: WITHDRAW, DEPOSIT, FEE, TRANSFER, INTEREST_ON_FREE_CASH, LENDING_INTEREST. Optional `time` filter |
| `POST /api/v0/equity/history/exports` | 1 / 30 s | Asks for a CSV report (in the client, not used by the app yet) |
| `GET /api/v0/equity/history/exports` | 1 / 1 min | Report status and `downloadLink` |

The old endpoints `account/info`, `account/cash`, `portfolio` and `portfolio/{ticker}` (used by T212Ext 1.x) are gone from the spec. Pies are deprecated.

## Rate limits

- **Per account**, whatever the key or IP. Bursts are allowed.
- Every response carries `x-ratelimit-limit`, `x-ratelimit-period`, `x-ratelimit-remaining`, `x-ratelimit-reset` (Unix time) and `x-ratelimit-used`.
- Reported on the forum (2026-09): `/history/exports` sometimes answers 429 without these headers and without `Retry-After`. T212Ext then waits the documented period.

## Pagination and its known problems

- History endpoints take `limit` (default 20, max 50) and `cursor`, and return `nextPagePath` until the last page.
- **Known problems, not fixed as of 2026-10:**
  - **Orders:** months of history missing for some accounts (forum, 2025-11).
  - **Transactions:** `nextPagePath` answers 404 from the third page on (2026-01/02).
  - `nextPagePath` is sometimes always null, or in another format.
  - One third-party fix suggests cancelled orders make pages get skipped.
- **What T212Ext does:**
  - It follows only the query string of `nextPagePath`, on the endpoint's own path, and stops on a cursor it has already seen.
  - CSV reports are the fallback, and are merged with what the API returned.

## What the API does not give

- **Withholding tax per dividend.** Only CSV reports have it, in the "Withholding tax" column.
- **Upcoming dividends:** no ex-dates and no pay dates. Only paid dividends exist. A dividend calendar needs another data source.
- **Portfolio history:** only the current state. T212Ext keeps a daily snapshot.
- **Sector, country or dividend yield** of instruments. The ISIN's first two letters give the country of issue.

## CSV reports

- Made in the app (**History → Export**) or through `/history/exports`. At most one year per report.
- A report only has the columns its rows use, so the parser reads columns by name.
- **The current layout**, checked against real reports exported on 2026-10-13 (2023–2026):
  - the header is `Action,Time (UTC),ISIN,Ticker,Name,Notes,ID,No. of shares,Price / share,Currency (Price / share),Exchange rate,Result,Currency (Result),Total,Currency (Total),Withholding tax,Currency (Withholding tax),Charge amount,Currency (Charge amount),Deposit fee,Currency (Deposit fee),Currency conversion fee,Currency (Currency conversion fee)`;
  - times carry an offset: `2025-01-01 03:56:57+00:00`.
- **Older layouts** (from known samples): the time column is plain `Time` with no offset, read as UTC; the currency may be in the header, as in `Total (EUR)`.
- **Dividend rows:**
  - `Action` is `Dividend (<type>)`. Seen: `Dividend (Dividend)`, `Dividend (Dividend manufactured payment)`, `Dividend (Tax exempted)`. Known from samples: `Dividend (Ordinary)`, `Dividend (Dividends paid by us corporations)`;
  - `No. of shares`;
  - `Price / share`: the **net** amount per share (after withholding), in `Currency (Price / share)`. Checked against known dividends: Apple May 2024 shows 0.2125 = 0.25 gross × 0.85. The API's `grossAmountPerShare` is the real gross;
  - `Total`: net, in the account currency;
  - `Withholding tax` with its currency (the instrument's). Filled on every dividend row seen, but **rounded to cents**, so tiny positions show 0.00. Shares × (API gross − CSV net per share) gives the exact amount;
  - Portuguese companies' dividends come with **35%** withheld (the omnibus-account rate).
- **Dividend rows have no `ID`**, so they are matched to the API's dividends by instrument, date, quantity and amount.
- **`Dividend adjustment`:** no instrument, an `ID`, and a note such as "2025 US dividends withholding tax refund adjustment". These are corrections of tax already withheld, usually refunds, so positive. T212Ext stores them as cash movements of type `DIVIDEND_ADJUSTMENT`, which the API does not have.
- **Cash rows:** `Deposit`, `Withdrawal`, `Interest on cash`, `Lending interest` (one row per day it pays), each with an `ID`.
- **First real import (2026-10-13, one Invest account, 2023-12 to 2026-10):**
  - all 531 dividend rows matched the 531 dividends the API had returned, and got their withholding tax;
  - all 548 cash rows, `Dividend adjustment` included, were already among the API's 567 transactions.

  So, for this account, the API's dividend and transaction history was complete, and cash row `ID`s seem to equal the API's `reference`. *To check:* which API `type` the dividend adjustments have. A reference match keeps the API's type.
- **Order rows** (`Market buy`, `Market sell`, `Limit …`) have an `ID` like `EOF…`. It is not the API's numeric order id.

## Terms of use

[API Terms](https://www.trading212.com/legal-documentation/API-Terms_EN.pdf), last updated 17.10.2025:

- **6.2:** the API is for your personal use. You may not make any part of it available to third parties.
- **6.3:** apps "intended for use by other end-users" need Trading 212's **prior written consent**; without it, access is revoked.
- **4.2:** no commercial services (agent, brokerage, asset management), no algorithmic trading, no repackaging or reselling of the API or its data, and no apps that replicate Trading 212.
- **7.1(e):** market data may not be redistributed.
- **9.1:** access can be cut at any time.
- The Help Centre says Trading 212 has no official partnerships with third-party apps.
- **In practice,** several apps connect with users' own keys (getquin; Sharesight and Wealthfolio through SnapTrade), and others take CSV reports only (Snowball, Delta).

**For T212Ext:** personal use is fine. Offering it to other people, as a service or a paid product, needs Trading 212's written consent. Without it, a product would have to be built on CSV imports, or on self-hosting where each person runs their own copy. See [BACKLOG.md](BACKLOG.md#product-ideas).
