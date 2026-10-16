# ADR 0004: Security hardening before going public

- **Status:** Accepted
- **Date:** 2026-10-16

## Context

Phases 1–7 secured each feature as it was built (ADR 0002: cookies, CSRF, lockout, login rate limit).
Before the app is reachable from the internet (Phase 9), the defaults around those features need to
fail closed, survive restarts and work behind a hosting platform's reverse proxy.

## Decisions

### Authorization: deny by default

- A **fallback policy** requires a signed-in user on every endpoint. Public endpoints say so with
  `[AllowAnonymous]`: the catalogue, guest-cart preview, register/login/logout and health checks.
- Back-office endpoints use the **`ManageStore` policy**, not `Roles = "Admin"`. A future Staff role
  can be granted some capabilities by changing policies, not controllers.
- **`SecurityTests` lists every route.** It fails if a route outside the allowlist becomes public, or
  if any `/api/admin/*` route lacks `ManageStore`.
- Requests that match no endpoint get **404 before authorization**. Otherwise the fallback policy
  would answer 401 for unknown URLs and missing images.

### Response headers

Added as the response starts, so error responses carry them too:

- `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: strict-origin-when-cross-origin`, a restrictive `Permissions-Policy` and
  `Cross-Origin-Opener-Policy: same-origin`.
- **API and media responses:** `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; …`.
  They are data, never pages. The web app's HTML gets its own policy (ADR 0005).
- **`Cache-Control: no-store`** on `/api` responses, unless an endpoint sets its own caching. Orders,
  wallets and addresses must never sit in a shared cache.
- HSTS and the HTTPS redirect (outside Development) are unchanged.

### Rate limits (`RateLimiting:*`, fixed one-minute windows)

| Limit | Key | Default | Applies to |
|---|---|---|---|
| Global | client IP | 600 | every `/api` request except health checks |
| Auth | client IP | 10 | register, login |
| Sensitive | user (IP if anonymous) | 20 | top-ups, place/cancel order, change password, bulk status, wallet adjustments |
| Reports | user | 30 | report pages and CSV exports |

- The global limit is generous on purpose. Indian mobile carriers put many customers behind one
  carrier-grade NAT address, so it is a flood guard, not a fairness tool.
- Rejections are **429 ProblemDetails with `Retry-After`**.
- Limits are **in-memory, per instance**. That is right for one instance. Several instances would
  need a shared store (Redis) or limits at the proxy.

### Cookie keys in the database

- Data Protection keys, which encrypt the auth cookie, are stored in **`data_protection_keys`**
  (`PersistKeysToDbContext`), under the application name `QuickKirana`.
- A redeploy, a restart or a free-tier sleep no longer signs everyone out, and every instance can read
  every cookie.
- **Trade-off:** the keys are stored unencrypted. Anyone with write access to the database could forge
  sessions, but they could already change any data directly. Encrypting the keys
  (`ProtectKeysWith…`) needs a certificate or key vault, which comes with a paid host.

### Reverse proxies (`ForwardedHeaders:*`)

- `X-Forwarded-For` and `X-Forwarded-Proto` are honoured **only from trusted proxies**, with
  `ForwardLimit = 1`. Otherwise any client could pick its own IP and slip past rate limits.
- Trust comes from `KnownProxies` and `KnownNetworks`.
- **`TrustAllProxies`** exists for platforms such as Render, where the container cannot be reached
  except through the platform's proxy. It is off by default.

### Fail fast at start-up

Invalid configuration stops the app with a clear message instead of starting half-broken:

- a missing connection string
- an admin seed password shorter than 12 characters in Production
- non-positive rate limits
- malformed delivery PIN codes

### Smaller attack surface

- **Request bodies are capped at 256 KB** (Kestrel). The product-image upload raises its own limit
  with `[RequestSizeLimit]`.
- **Health checks are split:**
  - `/api/health/live` checks the process only, so a database outage doesn't trigger restarts.
  - `/api/health/ready` checks the database.
  - `/api/health` is kept as an alias of ready.

### Dependencies

- `dotnet list package --vulnerable --include-transitive` and `npm audit` report nothing.
- The build already treats NuGet advisories as errors (NU1902/NU1903). This phase it caught
  vulnerable `System.Security.Cryptography.Xml` and `Pkcs` 10.0.9, pulled in by
  DataProtection.EntityFrameworkCore. They are pinned centrally to the patched runtime release.

## Consequences

- New endpoints are private until someone deliberately makes them public, and the route test makes
  that a visible, reviewed change.
- Rate limits and proxy trust are configuration. Phase 9 sets `ForwardedHeaders:TrustAllProxies=true`
  on Render.
- **Not done (deliberately):**
  - a Staff role (declined for now; policies make it cheap later)
  - distributed rate limiting
  - key encryption at rest
  - a Content-Security-Policy for the SPA's HTML (done in Phase 9, ADR 0005)
