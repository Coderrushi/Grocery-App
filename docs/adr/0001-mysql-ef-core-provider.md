# ADR 0001: MySQL provider for EF Core 10

- **Status:** Accepted
- **Date:** 2026-10-15

## Context

The project requires .NET 10, EF Core 10 and MySQL. EF Core talks to MySQL through a third-party
provider, and a provider supporting .NET 10 does not automatically support EF Core 10.

Options checked on NuGet on 2026-10-15:

| Provider | Latest | EF Core 10 build? |
|---|---|---|
| Pomelo.EntityFrameworkCore.MySql | 9.0.0 | No. No EF Core 10 release or preview exists. |
| MySql.EntityFrameworkCore (Oracle) | 10.0.9 (2026-07-29) | Yes. `net10.0` target, built against EF Core 10.0.9. |

## Decision

Use **MySql.EntityFrameworkCore 10.0.9**. Pin EF Core and every package that depends on it
(`Microsoft.EntityFrameworkCore.*`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`,
`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `dotnet-ef`) to **exactly
10.0.9** via `EfCoreVersion` in `Directory.Packages.props`.

ASP.NET Core packages that do not depend on EF Core use `AspNetCoreVersion` (currently 10.0.13)
so they continue to receive security patches. One example: `Microsoft.AspNetCore.OpenApi` 10.0.9
pulls in Microsoft.OpenApi 2.0.0, which has a known high-severity vulnerability
(GHSA-v5pm-xwqc-g5wc), and 10.0.13 fixes it.

**Upgrade rule:** bump `EfCoreVersion` only when Oracle publishes a matching provider release,
then run the full test suite, including `ProviderSpike`.

## Verified behaviour

These are covered by `tests/GroceryStore.Tests.Integration/ProviderSpike/MySqlProviderTests.cs`,
which runs in CI on every build:

- Unicode (Hindi and emoji) round-trips with utf8mb4.
- `decimal(10,2)` money is exact. MySQL rounds extra places half away from zero.
- UTC `DateTime` round-trips with microsecond precision. `UtcDateTimeConverter` restores `Kind = Utc`.
- Case-insensitive `Contains` search, paging, and a `GroupBy(date)` + `Sum` report query translate to SQL.
- Rollback leaves no trace.
- `SELECT … FOR UPDATE` (via `FromSql`) prevents overselling under 12 concurrent buyers.
- A conditional `ExecuteUpdateAsync(... WHERE Stock >= qty)` also prevents overselling.
- Concurrency tokens raise `DbUpdateConcurrencyException` on lost updates.

## Provider quirks and the rules they impose

1. **Charset/collation must not be declared in the EF model.** `HasCharSet`/`UseCollation` on the
   model makes the provider generate migration code that does not compile (a missing `using`,
   then an ambiguous `UseCollation` call). Instead, the database is created with
   `utf8mb4 / utf8mb4_0900_ai_ci` and tables inherit it. An integration test checks every table.
2. **`DateTimeOffset` maps to `datetime` with no offset and no fractional seconds.** Entities use
   UTC `DateTime` only. Identity's `LockoutEnd` is the one exception, and it is harmless.
3. **Retrying execution strategy.** `EnableRetryOnFailure` is on, so any explicit transaction must
   be wrapped in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`. Without the wrapper, the
   first query or save inside the transaction throws ("does not support user-initiated
   transactions"), and a test proves it.
4. **Keys are `int` AUTO_INCREMENT**, avoiding the provider's `Guid` mapping differences.
5. **Deadlocks are not retried by default.** Found in Phase 6: MySQL error 1213 (deadlock) reached
   the client as a 500. `EnableRetryOnFailure` now lists 1213 and 1205 (lock wait timeout)
   explicitly. Retried transaction bodies must rebuild their state on each attempt
   (`ChangeTracker.Clear()` and reload).
6. **Foreign-key checks take shared locks.** Inserting a child row (for example `order_items` →
   `products`) share-locks the parent. Two transactions that do this and then update the same parent
   deadlock. Rule: **update the contended parent rows first** (stock), in a consistent order
   (ascending id), then insert the child rows.

## Alternatives if the provider becomes a blocker

- **EF Core 9 + Pomelo 9** on the .NET 10 runtime: mature, but breaks the EF Core 10 requirement.
- **Dapper + MySqlConnector** for specific hot paths (for example reports), alongside EF Core.
