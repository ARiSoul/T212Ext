# PostgreSQL

T212Ext moved from SQL Server to PostgreSQL in **3.0.0** (2026-10-27), as the proof of concept before traceon does the same (its 2.0.0). This file is what was decided, what was learned and what to watch for. It is written for the next move as much as for this repository.

## Contents

1. [Decisions](#1-decisions)
2. [Dates and times](#2-dates-and-times)
3. [Text: case and collation](#3-text-case-and-collation)
4. [Migrations](#4-migrations)
5. [Moving the data](#5-moving-the-data)
6. [Tests and CI](#6-tests-and-ci)
7. [Backups and restore](#7-backups-and-restore)
8. [Upgrades](#8-upgrades)
9. [Surprises](#9-surprises)
10. [For traceon](#10-for-traceon)

## 1. Decisions

- **One PostgreSQL per app**, in the app's own compose project: `t212ext-db`, its own volume (`pgdata`), its own password (`T212EXT_PG_PASSWORD` in `.env`), reached only by `t212ext-web` on the project's default network. No published port, no shared instance.
  - *Why:* with a shared SQL Server, restarting traceon's database stopped T212Ext, and `docker compose down -v` in `~/traceon` would have deleted T212Ext's data (traceon `docs/OPERATIONS.md`, section 20). Now the apps share only the reverse proxy (network `edge`).
- **The latest stable version**: `postgres:18` (18.6 when moved; 19 is due on 2026-11-19, a dump and restore, section 8).
- **Locale: the builtin provider, `C.UTF-8`** (`POSTGRES_INITDB_ARGS=--locale-provider=builtin --builtin-locale=C.UTF-8`). Text compares and sorts by code point, identically on every server and image version.
  - *Why:* collations from glibc or ICU can change with an operating system or library update, and then indexes on text silently go out of order until reindexed. The builtin provider never changes. The cost: sorting is by code point (`B` before `a`, accents after `z`), so human ordering is done in .NET (T212Ext sorts its lists there anyway).
- **Codes are normalised in the app** (section 3), rather than an ICU nondeterministic collation.
- **The data moves with a command of the app**, `copy-from-sqlserver`, not pgloader (section 5).
- **SQL Server stays untouched** as the way back until the move is confirmed; dropping the old database and login is done by hand afterwards (docs/UPGRADING.md).

## 2. Dates and times

| .NET type | PostgreSQL type | Rule |
|---|---|---|
| `DateTimeOffset` | `timestamptz` | Always written in UTC (below). 27 columns. |
| `DateOnly` | `date` | As is. |
| `TimeOnly` | `time` | As is (quiet hours). |
| `DateTime` | — | Not used. |

**Npgsql writes `timestamptz` only from a `DateTimeOffset` with offset 0**, and throws otherwise (`Cannot write DateTimeOffset with Offset=01:00:00 to PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported`). Values with an offset can come from the Trading 212 API's JSON and from CSV files. So one model-wide convention (`T212ExtDbContext.ConfigureConventions`) converts every `DateTimeOffset` and `DateTimeOffset?` to UTC on the way in, **parameters of queries included**: a comparison with a `+02:00` value works, and finds the same instant. Values read back have offset 0.

- Nothing else had to change: the app already computed in UTC (`TimeProvider.GetUtcNow()`, `new DateTimeOffset(…, TimeSpan.Zero)`), and shows local time only on screen (`Fmt.DateTime`).
- **For `DateTime`** (traceon): with `timestamptz` Npgsql requires `Kind=Utc` and throws otherwise. Decide per column: `timestamptz` for instants (convert to UTC, as here), `timestamp` (without time zone) for wall-clock values that have no instant. Do not turn on Npgsql's legacy timestamp behaviour: it hides the problem.
- Test: `PostgreSqlTests.An_instant_with_any_offset_is_stored_and_compared_in_utc`.

## 3. Text: case and collation

**SQL Server's default collation is case-insensitive**, and nothing in T212Ext ever set one. So equality, unique indexes and `ORDER BY` were case-insensitive without anyone deciding it. **PostgreSQL is case-sensitive.**

**The audit** (every string compared in a query, or in a unique index):

| Value | Where it comes from | Case |
|---|---|---|
| ISIN | API, CSV files, the stock split form, page addresses | Upper case; now enforced where it comes in (`Domain/Codes.cs`) |
| Currency, country codes | API, CSV files, forms | Upper case; enforced the same way |
| Ticker (`AAPL_US_EQ`, `VUSAl_EQ`) | Trading 212 only | Mixed case by design, always the same; kept as is |
| Account `Key` | `.env` | As typed, compared with itself (copied as is) |
| Dividend and transaction references, report ids | Trading 212 | As given |
| Hashes (CSV row, push endpoint) | The app | Always one case |
| Types (`ORDINARY`, `InterestOnFreeCash`…) | API constants, CSV mapped to them | One spelling |

**Choice: explicit normalisation** in the app (`Codes.Upper` where ISINs and codes come in, and in the lookup by address), with PostgreSQL's ordinary, case-sensitive behaviour. Indexes stay plain and every comparison is predictable. A nondeterministic ICU collation would have kept SQL Server's behaviour without code changes, but with limits (some operators and index types) and behaviour that is harder to reason about.

**Ordering** that the UI relies on is done in .NET (grids sort in memory, `OrderBy` on names after the query). The few `ORDER BY` on text in SQL (accounts by name) do not care about case.

## 4. Migrations

The ten SQL Server migrations could not run on Npgsql (they carry SQL Server annotations and types). They were **replaced by one `InitialCreate`** generated for Npgsql; the old ones stay in git history (tag `v2.18.0`). A fresh database is created from it; the production data comes over with the copy (section 5), whose target is migrated first.

What the model became: `numeric(28,10)` for decimals, `character varying(n)` for strings with a maximum length, `bytea` for logos, `boolean`, identity columns (`GENERATED BY DEFAULT AS IDENTITY`). No raw SQL in the app needed a change; EF quotes every identifier (`"Dividends"`), so hand-written SQL must too.

## 5. Moving the data

**`copy-from-sqlserver`** (a command of the image, like `hash-password`; `Web/CopyFromSqlServer.cs`, `Core/Data/DatabaseCopy.cs`):

- Reads with the SqlServer provider (`ConnectionStrings:SqlServer`) and writes with Npgsql (`ConnectionStrings:T212Ext`), **through the EF model**, so values convert as the app writes them (instants to UTC, decimals exactly).
- **Foreign-key order** from the model (principals first), **one transaction**: a failure leaves the target empty. Owned rows (positions of a snapshot, taxes of an order) go with their owner.
- **Refuses a target that has rows**; migrates it first.
- **Moves every identity sequence past the highest copied key** (`setval(pg_get_serial_sequence(…), max(id))`). Without it, keys are copied but the sequences stay at 1, and the next insert fails on a duplicate key. This is the classic trap.
- **Compares the row count of every table** in both databases, with SQL that runs on both: `SELECT CAST(COUNT(*) AS bigint) AS "Value" FROM "Table"` (SQL Server's `COUNT` is an `int`; quoted names work on both).
- In production it runs once, from a compose file added for that run (`ops/postgres/copy-from-sqlserver.yml`): only the copy joins the network `data`, where SQL Server is; the app never does again.
- Tested against a real SQL Server (LocalDB) into PostgreSQL 18: every count, sums and first and last dates equal, the sequence at the highest id. The generic copy is tested in CI PostgreSQL to PostgreSQL (`PostgreSqlTests`).
- **pgloader** was the alternative: nothing to keep in the code, but `datetimeoffset`, the decimals and the sequences would have been ours to check, untested.

The command and the SQL Server package go away once the move is confirmed (backlog).

## 6. Tests and CI

- **Tests**: a `postgres:18` container started for the run by **Testcontainers** (`Testcontainers.PostgreSql`), with the production locale; or `T212EXT_TEST_PG` (a connection string; the fixture uses its own database on it). LocalDB is gone. Docker must be running for the tests.
- **CI**: a `postgres:18` service container, through `T212EXT_TEST_PG`. The image's smoke test starts the app against a PostgreSQL container (migrations included), and checks that `copy-from-sqlserver` is in the image.
- Testcontainers 4.x takes the image in the builder's constructor (`new PostgreSqlBuilder("postgres:18")`).
- EF's analyser refuses interpolated raw SQL (`EF1002`); table names from the model are the one place it is suppressed, with the reason.

## 7. Backups and restore

- **Backup**: `pg_dump --format=custom` (compressed), run inside `t212ext-db` with `docker exec`: the image trusts its local socket, so no password is passed anywhere. The archive is checked with `pg_restore --list` before it is encrypted (gpg, as before).
- **Restore test**: `pg_restore` into a throwaway database (`t212ext_restore_test`), exact row counts of every table compared with the live database, then `DROP DATABASE … WITH (FORCE)`.
- **Disaster restore**: recreate the database and `pg_restore` into it (docs/OPERATIONS.md, section 7).
- `PGOPTIONS='-c client_min_messages=warning'` keeps the scripts' output to what matters.

## 8. Upgrades

- **Minor versions** (18.6 → 18.7): `docker compose pull t212ext-db && docker compose up -d t212ext-db`. Same data format.
- **Major versions** (18 → 19): a dump and restore. The `postgres` image from 18 keeps its data in `/var/lib/postgresql/<major>/docker`, and the volume is mounted at `/var/lib/postgresql`: a 19 container starts empty beside the 18 data, which stays as the way back. Steps in docs/OPERATIONS.md, section 8.

## 9. Surprises

- **Nothing in the queries.** With the date convention in place, all 199 existing tests passed on PostgreSQL at the first run: EF translated every query, and the app's pages ran against the copied data without an error. The work was around the database, not in it.
- **Dates were the one real risk**, and one convention covered it (section 2).
- **Case**: no bug found, because every compared value already came in one case; the normalisation makes that a rule instead of luck.
- **Owned collections have no `DbSet`**, so counting them for the comparison went through SQL.
- **The `postgres:18` image changed its data path** (`/var/lib/postgresql/18/docker`); mounting the parent keeps major upgrades side by side.
- **Sequences after a copy with explicit keys** (section 5).
- **One "Failed executing DbCommand" at the first start** against an empty database: EF reads `"__EFMigrationsHistory"` before it exists, logs the error, then creates it and applies the migrations. Expected; it does not come back.

## 10. For traceon

- Its OPERATIONS section 20 (the contract with the other apps) must be updated in the traceon repository: T212Ext no longer uses SQL Server nor the network `data`; it shares only `edge` and the Caddy site file.
- Traceon's own questions, beyond what is here: an accent-insensitive search (an ICU nondeterministic collation, or `unaccent` with a generated column and an index), the Kusto to T-SQL log explorer, and 209 migrations (the same restart with one `InitialCreate` applies).
