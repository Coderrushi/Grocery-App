# ADR 0002: Cookie-based authentication with ASP.NET Core Identity

- **Status:** Accepted
- **Date:** 2026-10-15

## Context

The React app and the API are served from one site: Vite proxies `/api` in development, and a
reverse proxy does the same in production. Customers sign in with an email address or a mobile
number. The shopkeeper needs an admin role. Native mobile apps may come later.

## Decision

- **ASP.NET Core Identity** stores users, password hashes, roles and lockout state in MySQL.
- **Our own `/api/auth/*` endpoints** handle register, login, logout and `me`, instead of
  `MapIdentityApi`. The built-in endpoints can't capture full name or phone, can't log in by phone,
  and return responses that don't match our ProblemDetails format.
- **Session = HttpOnly, `SameSite=Strict`, Secure (production) cookie** `qk_auth`, valid for 14 days
  with sliding expiration. "Keep me signed in" unticked gives a browser-session cookie.
- **CSRF** protection comes from `SameSite=Strict` plus JSON-only request bodies. A cross-site page
  cannot attach the cookie, and an HTML form cannot send `application/json`, so the API answers 415.
  No anti-forgery tokens are needed.
  - Endpoints that must accept `multipart/form-data` (image uploads) carry `[RequireXhrHeader]`. An
    HTML form can post multipart data, but it cannot add the `X-Requested-With` header, and a
    cross-site script that adds it triggers a CORS preflight the API never approves. The web app's
    `apiClient` sends the header on every request.
- **Passwords:** at least 8 characters and at least 4 distinct characters, with no composition rules
  (NIST SP 800-63B). Lockout after 5 failures for 15 minutes.
- **Rate limiting:** 10 requests per minute per IP on register and login. Phase 8 added more limits
  (ADR 0004).
- **The admin account** is created at start-up from `AdminSeed:*` configuration, never from a migration.

## Consequences

- Tokens never touch JavaScript, so an XSS bug cannot steal a session.
- The API answers 401/403 (as ProblemDetails) rather than redirecting to a login page.
- **Deployment requirements:** done in Phase 8 ([ADR 0004](0004-security-hardening.md)). Data
  Protection keys are stored in the database, and forwarded headers are trusted only from configured
  proxies.
- **A future native app** can enable Identity bearer tokens alongside cookies without changing user
  storage.
- **Password changes** sign out other devices within 30 minutes (the security-stamp validation interval).
