# ADR 0005: Free-tier deployment on Render, Aiven and Cloudinary

- **Status:** Accepted
- **Date:** 2026-10-16

## Context

The shop needs to be online at no cost while it proves itself. The stack is ASP.NET Core 10, MySQL 8
and a React SPA that relies on a same-origin, `SameSite=Strict` auth cookie (ADR 0002). Most free
hosts offer only PostgreSQL, or don't run .NET natively. Free disks are wiped on restart.

## Decision

| Concern | Choice | Why |
|---|---|---|
| App hosting | **Render** free web service, built from the repo's `Dockerfile` | Runs any container; HTTPS and auto-deploy from GitHub; no expiry |
| Database | **Aiven for MySQL** free plan (1 CPU, 1 GB RAM, 1 GB storage) | Real MySQL 8 (ADR 0001 holds); no card; no expiry |
| Product photos | **Cloudinary** free plan, behind `IMediaStorage` | The app's disk is ephemeral; images are served resized from a CDN |
| Configuration | `render.yaml` Blueprint; secrets entered in Render's dashboard (`sync: false`) | Infrastructure in git, secrets never in git |

### One origin

- **The API serves the built React app.** The Docker image copies `web/dist` to `wwwroot`.
  `SpaHosting` serves those files and sends `index.html` for any other non-API path.
  - The cookie keeps working without CORS, exactly as with Vite's proxy in development.
  - Unknown `/api` and `/media` URLs still return 404 ProblemDetails, never the app.
- **Caching:**
  - Files under `/assets` have content-hashed names, so they are cached for a year.
  - `index.html` is `no-cache`, so a deploy reaches users on their next visit.
- **A strict Content-Security-Policy on pages:** `script-src 'self'` plus the SHA-256 of the one
  inline script (the dark-mode bootstrap in `index.html`).
  - The hash is computed from the deployed file at start-up, so the policy can't drift from the build.
  - When Cloudinary is used, `img-src` also allows `https://res.cloudinary.com`.

### Migrations

- **`Database:MigrateOnStartup=true` on Render.** The free plan has no pre-deploy hook and runs one
  instance. EF Core takes a database lock while migrating, so an overlapping start waits.
- Locally and in CI tests, migrations are still applied explicitly.
- If the service ever runs several instances or a paid pre-deploy step, switch to an EF migration
  bundle run before release.

### Database connection security

- Aiven requires TLS (`SslMode=Required`).
- **Setting `Database:CaCertificatePem`** (Aiven's `ca.pem`) upgrades the connection to `VerifyCA`,
  so the server's certificate is checked and a man-in-the-middle is detected, not only encryption.

### Images

- `Media:Provider=Cloudinary` with `Media:CloudinaryUrl` (`cloudinary://key:secret@cloud`).
- **Uploads are signed** with the API secret, so Cloudinary's REST API is called directly with no
  SDK. The signing code is tested against Cloudinary's documented example.
- **Delivery URL:** `c_limit,w_800,f_auto,q_auto`: at most 800 px wide, WebP or AVIF for browsers
  that support them.
- Development keeps the local `/media` folder.

### Container

- **Three build stages:** node builds the web app, the .NET SDK publishes the API, and the
  `aspnet:10.0` image runs it.
- It runs as the image's non-root user and listens on port 8080 (`PORT=8080` tells Render).
- `DOTNET_gcServer=0` keeps memory use small on a 512 MB instance.

### CI

- A **`container` job** builds the same image and starts it against a MySQL service container, with
  migrations on start-up and demo data.
- It then smoke-tests readiness, the catalogue, the page CSP, React routing and the API 404.
- Docker is not needed on developer machines: a broken image fails in CI, not on Render.

## Consequences and limits of the free tier

- **Cold starts.**
  - Render stops the service after 15 minutes without traffic, and the next visit waits about 30–60
    seconds.
  - Aiven may power off a free database that shows no sustained activity. It notifies first, and the
    database can be powered on again from its console.
- **Latency.** Aiven's free plan does not let you choose a region. Put the Render service in the
  region nearest the database: every page makes several queries.
- **Capacity.**
  - 1 GB of database storage and about 76 MySQL connections.
  - One app instance, which matches the in-memory rate limits (ADR 0004).
  - 5–100 GB of Render bandwidth a month (sources differ).
- **Backups.** Aiven's free plan includes backups but no SLA. Export important data (the CSV
  reports, or `mysqldump`) regularly until the shop moves to a paid plan.
- **Moving to paid plans later** is a configuration change: a bigger Render instance, a pre-deploy
  migration bundle, a paid Aiven plan. The code doesn't change.
