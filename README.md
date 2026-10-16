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

## Deploying (Render + Aiven + Cloudinary)

The whole shop runs free on three services. [ADR 0005](docs/adr/0005-deployment.md) explains the
choices and the free-tier limits. You need a GitHub account and about 30 minutes. Never paste a
password or key into a file in this repository: every secret goes into Render's dashboard.

1. **Put the code on GitHub.**
   - On github.com, create an empty **private** repository, for example `quickkirana`, without a
     README.
   - Then push to it:
     ```powershell
     git remote add origin https://github.com/<you>/quickkirana.git
     git push -u origin main
     ```
   - The CI workflow runs on every push. It builds and tests the API and web app, and builds and
     smoke-tests the Docker image.
2. **Create the database (Aiven).**
   - Sign up at aiven.io, then **Create service → MySQL → Free plan**.
   - When it's running, the overview page shows **Host, Port, User (`avnadmin`), Password** and
     **Database (`defaultdb`)**. Download the **CA certificate** too.
   - Note the service's **cloud region**.
   - Your connection string (keep it private):
     `Server=<host>;Port=<port>;Database=defaultdb;User=avnadmin;Password=<password>;SslMode=Required`
3. **Create the image store (Cloudinary).**
   - Sign up at cloudinary.com.
   - On the dashboard, copy the **API environment variable**: `cloudinary://<key>:<secret>@<cloud>`.
4. **Pick the region.** In `render.yaml`, set `region` to the Render region closest to the Aiven
   region (`singapore`, `frankfurt`, `oregon`, `ohio` or `virginia`). Commit and push.
5. **Create the service (Render).**
   - Sign up at render.com with GitHub, then **New → Blueprint** and pick the repository.
   - Render reads `render.yaml` and asks for the secrets:

     | Setting | Value |
     |---|---|
     | `ConnectionStrings__GroceryDb` | the connection string from step 2 |
     | `Database__CaCertificatePem` | the whole text of Aiven's `ca.pem`, `-----BEGIN…` to `…END CERTIFICATE-----` |
     | `Media__CloudinaryUrl` | the `cloudinary://…` value from step 3 |
     | `AdminSeed__Email` and `AdminSeed__Password` | the shopkeeper's sign-in; password at least 12 characters |
6. **Deploy.**
   - Render builds the image (5–10 minutes the first time). On start-up the app creates the tables,
     the admin account and, with `SeedDemoCatalog=true`, about 40 demo groceries.
   - When the health check passes, open `https://<service>.onrender.com` and sign in as the admin.
7. **After the first start:**
   - You may delete `AdminSeed__Password` in Render. The account exists, and the seeder never
     changes an existing password.
   - For a real shop, set `SeedDemoCatalog` to `false` before adding real products.

**Updating:** push to `main`. CI runs, and Render redeploys automatically. Migrations apply on
start-up.

**If something fails:** check Render's **Logs** tab. The app refuses to start with a clear message
when a setting is missing or invalid, for example a short admin password or a bad Cloudinary URL.
`/api/health/live` shows the process is up. `/api/health/ready` also checks the database.

## Conventions

- **Errors:** every API error is RFC 7807 ProblemDetails with a `traceId`. Services throw
  `NotFoundException`, `ConflictException` or `BusinessRuleException`, and never return error codes.
- **Validation:** FluentValidation validators next to their request DTOs run automatically.
- **Security ([ADR 0004](docs/adr/0004-security-hardening.md)):**
  - Every endpoint requires sign-in unless marked `[AllowAnonymous]`, and back-office controllers use
    `[Authorize(Policy = Policies.ManageStore)]`. Making an endpoint public means adding it to the
    allowlist in `SecurityTests`.
  - Endpoints that move money or change accounts get `[EnableRateLimiting(RateLimits.Sensitive)]`.
  - Health checks: `/api/health/live` (the process) and `/api/health/ready` (the database).
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
- **Media:** go through `IMediaStorage` (local `/media` folder in development, Cloudinary in
  production). Validate uploads by content (`ImageFile.DetectExtension`),
  never by file name.
- **Reports:** `ReportService` computes everything live with GROUP BY queries. "Sales" means orders
  placed in the period, minus cancelled ones, counted on their IST day (`ShopTime`). Periods are
  `?from=&to=` IST dates, both included, at most 366 days. CSV downloads go through `CsvBuilder`
  (UTF-8 BOM, invariant numbers, formula-injection guard). Charts are hand-written SVG in
  `features/reports/charts.jsx` using the `chart-bar` colour token.
- **Time:** store UTC `DateTime` (never `DateTimeOffset`), and display in IST via `formatDateTime`.
  Calendar days (report dates) are `DateOnly` / "yyyy-mm-dd" strings, formatted with `formatDay`.
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
