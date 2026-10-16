# QuickKirana

QuickKirana is an online ordering and doorstep delivery app for a local grocery store, with a customer storefront and an admin
back office.

| Layer | Technology |
|---|---|
| API | ASP.NET Core 10 Web API, EF Core 10, ASP.NET Core Identity |
| Database | MySQL 8.x (8.4 LTS in production) via `MySql.EntityFrameworkCore` ([ADR 0001](docs/adr/0001-mysql-ef-core-provider.md)) |
| Web | React 19 + Vite, React Router, TanStack Query, Tailwind CSS v4 |
| Tests | xUnit v3 (unit + integration against real MySQL), Vitest + Testing Library |

## Repository layout

```
src/GroceryStore.Api/          ASP.NET Core API (modular monolith)
  Common/                      cross-cutting: errors, validation, health
  Data/                        AppDbContext, conventions, migrations
  Modules/<Module>/            one folder per business module (Identity, Catalog, Cart, Orders…)
tests/GroceryStore.Tests.Unit/
tests/GroceryStore.Tests.Integration/
  ProviderSpike/               regression tests pinning MySQL provider behaviour
web/                           React storefront and admin (Vite)
  src/app/                     router, layouts, providers
  src/features/<feature>/      feature components, hooks, API calls
  src/shared/                  api client, UI kit, utilities
database/                      one-time SQL scripts
docs/adr/                      architecture decision records
```

## Local setup

Prerequisites: .NET SDK 10.0.2xx, Node 24, MySQL 8.x.

1. **Create the database and app account.** Copy `database/dev-setup.sql`, replace `CHANGE_ME` with a
   password, and run it as root in MySQL Workbench.
2. **Store the connection strings in user-secrets.** They are kept outside the repository and never committed:
   ```powershell
   cd src/GroceryStore.Api
   dotnet user-secrets set "ConnectionStrings:GroceryDb"     "Server=localhost;Port=3306;Database=grocery_dev;User=grocery_app;Password=<pw>"
   dotnet user-secrets set "ConnectionStrings:GroceryTestDb" "Server=localhost;Port=3306;Database=grocery_test;User=grocery_app;Password=<pw>"
   ```
3. **Configure the shopkeeper's admin account.** It is created on the first API start:
   ```powershell
   dotnet user-secrets set "AdminSeed:Email"    "admin@quickkirana.local"
   dotnet user-secrets set "AdminSeed:Password" "<a long password>"
   ```
4. **Restore tools and apply migrations:**
   ```powershell
   dotnet tool restore
   dotnet ef database update --project src/GroceryStore.Api
   ```
5. **Run the API** (http://localhost:5230, Swagger UI at `/swagger`). In Development, an empty
   catalog is filled with about 40 demo groceries (`SeedDemoCatalog` in `appsettings.Development.json`).
   Uploaded product images are saved in `src/GroceryStore.Api/media/`, which git ignores.
   ```powershell
   dotnet run --project src/GroceryStore.Api
   ```
6. **Run the web app** (http://localhost:5173; `/api` is proxied to the API):
   ```powershell
   cd web
   npm install
   npm run dev
   ```

## Everyday commands

| Task | Command |
|---|---|
| All .NET tests | `dotnet test` |
| Unit tests only | `dotnet test --project tests/GroceryStore.Tests.Unit` |
| New migration | `dotnet ef migrations add <Name> --project src/GroceryStore.Api --output-dir Data/Migrations` |
| Review migration SQL | `dotnet ef migrations script --project src/GroceryStore.Api` |
| Web tests / lint / format | `npm test`, `npm run lint`, `npm run format` (in `web/`) |

## Conventions

- **Errors:** every API error is RFC 7807 ProblemDetails with a `traceId`. Services throw
  `NotFoundException`, `ConflictException` or `BusinessRuleException`, and never return error codes.
- **Validation:** FluentValidation validators next to their request DTOs run automatically.
- **Money:** `decimal(10,2)` in INR, inclusive of taxes. Every product has a selling `Price` and the
  printed `Mrp` (`Mrp >= Price`, enforced by a DB CHECK constraint). Format in the UI only via
  `formatCurrency`.
- **Stock:** change it only through `InventoryService` (an atomic conditional UPDATE plus an
  `inventory_adjustments` ledger row). Never set `StockQuantity` directly. Products are deactivated,
  never deleted.
- **Admin edits** carry the product `Version`; a stale version returns 409 instead of overwriting.
- **Cart:** priced only by the API (`CartService.PriceAsync` + `CartPricing`), always at current
  prices and stock. Guests keep `[{ productId, quantity }]` in localStorage, priced by
  `/api/cart/preview` and merged into the account at sign-in. Delivery rules live in `DeliveryCharges`
  in `appsettings.json`. The app refuses to start if those values are invalid.
- **Wallet:** move money only through `WalletService.CreditAsync`/`DebitAsync`, inside a
  transaction ([ADR 0003](docs/adr/0003-wallet-ledger.md)). Give every event-driven movement a
  unique `Reference` (`topup:{id}`, `order:{id}:payment`) so it can never be applied twice.
  Payments are simulated (`SimulatedPaymentGateway`); a real provider replaces that one
  `IPaymentGateway` registration. Limits are in `Wallet` in `appsettings.json`.
- **Orders:** `OrderService.PlaceOrderAsync` is a single transaction that re-prices, checks the
  customer's expected total, reserves stock, inserts items, debits the wallet and empties the cart.
  Status changes go only through `TransitionAsync`, which enforces `OrderWorkflow`. Cancelling
  refunds and restocks in the same transaction. Retried transaction bodies clear the change tracker
  and reload. Update contended rows (stock) before inserting rows that reference them
  ([ADR 0001](docs/adr/0001-mysql-ef-core-provider.md), quirks 5–6). Delivery coverage is
  `DeliveryArea:ServiceablePincodes` (empty = everywhere).
- **Media:** go through `IMediaStorage`. Validate uploads by content (`ImageFile.DetectExtension`),
  never by file name.
- **Time:** store UTC `DateTime` (never `DateTimeOffset`), and display in IST via `formatDateTime`.
- **Transactions:** wrap explicit transactions in `CreateExecutionStrategy().ExecuteAsync(...)`.
- **Tables:** lower snake_case and plural. Each entity configuration calls `ToTable("order_items")`
  explicitly, because EF otherwise uses the singular entity name.
- **Auth:** HttpOnly cookie session ([ADR 0002](docs/adr/0002-cookie-authentication.md)). Protect
  endpoints with `[Authorize]` or `[Authorize(Roles = Roles.Admin)]`, and scope every customer query
  by `User.GetUserId()`.
- **Validation error keys** are camelCase JSON names, so the web app maps them to form fields with
  `applyServerErrors`.
- **Web routes:** storefront pages are in the main bundle. Account and admin pages use `lazy` routes.
- **Themes (Light / Dark / System):** use the design tokens (`bg-surface`, `text-ink`,
  `text-brand-700`…), never raw colours. `index.css` redefines every token under
  `[data-theme='dark']`, and `src/shared/theme/theme.js` sets that attribute ("System" follows the OS
  live). On coloured backgrounds use `text-white`, not the light shades of a scale, because those
  turn dark in dark mode. Keep text at 4.5:1 contrast or better in both themes.
- **Packages:** versions live only in `Directory.Packages.props`. EF Core stays pinned to the
  MySQL provider's version.
