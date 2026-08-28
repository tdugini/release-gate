# ReleaseGate deployment

ReleaseGate supports two deployment modes with different purposes:

1. **VPS portfolio deployment** — the root `docker-compose.yml`, used by the automated Traefik-backed public demo;
2. **production-like integration deployment** — `docker-compose.prod.yml`, used by CI and optional full-auth self-hosted testing.

The VPS path is the canonical portfolio deployment.

## VPS portfolio architecture

```text
Browser
   |
   | HTTPS
   v
Traefik
   |
   v
ReleaseGate app :8080
React SPA + ASP.NET Core API
   |
   v
PostgreSQL :5432
```

The root `Dockerfile` builds the React application first, copies the generated assets into the ASP.NET Core `wwwroot`, publishes the API and produces one runtime container.

The root `docker-compose.yml` starts two services:

- `app` — combined React + ASP.NET Core application;
- `postgres` — persistent PostgreSQL database.

The app joins the private `releasegate-internal` network and the existing external Traefik network `docker_frontend_wp`.

PostgreSQL joins only `releasegate-internal` and does not publish port `5432` to the host.

## Traefik routing

The app is discovered by Traefik through Compose labels.

The current configuration expects:

- external Docker network: `docker_frontend_wp`;
- entrypoint: `https`;
- certificate resolver: `letsencrypt`;
- application container port: `8080`;
- default hostname: `releasegate.maytech.it`.

The hostname can be overridden through:

```text
RELEASEGATE_HOST
```

The corresponding DNS record must point to the VPS before the public URL can work. DNS creation is intentionally external to the ReleaseGate deployment workflow.

## Portfolio demo mode

The VPS stack runs the application with:

```text
DemoMode__Enabled=true
```

Demo mode:

- serves the compiled React SPA directly from ASP.NET Core;
- provides SPA fallback routing;
- applies committed EF Core migrations;
- seeds representative data when the database is empty;
- configures a dedicated **Portfolio Demo** control-plane principal.

The demo principal has no `operator` or `reviewer` roles.

This means a reviewer can navigate the control plane without entering a token, while mutation and approval endpoints remain protected by normal API authorization and return `403 Forbidden`.

The browser-visible demo token is intentionally read-only. It must never be reused as a privileged operator, reviewer or runtime credential.

## Automated VPS deployment

Deployment is handled by:

```text
.github/workflows/deploy-vps.yml
```

The workflow does not run directly on every push. It listens for completion of the `CI` workflow on `main` and deploys only when both conditions are true:

```text
CI conclusion == success
VPS_DEPLOY_ENABLED == true
```

This ensures the VPS receives a revision that has already passed the complete CI pipeline.

### Required repository secrets

Configure these GitHub Actions repository secrets:

```text
SRV_KEY
SRV_USRSRV
RELEASEGATE_POSTGRES_PASSWORD
```

`SRV_KEY`
: SSH private key used to connect to the VPS. The workflow expects the same PuTTY/PPK format used by the existing VPS deployment infrastructure and converts it to OpenSSH format at runtime.

`SRV_USRSRV`
: SSH destination, for example `deploy@example-vps`.

`RELEASEGATE_POSTGRES_PASSWORD`
: Strong password dedicated to the ReleaseGate PostgreSQL role.

Do not commit these values to the repository.

### Repository variables

Enable deployment with:

```text
VPS_DEPLOY_ENABLED=true
```

Optionally configure:

```text
RELEASEGATE_HOST=releasegate.maytech.it
```

If `RELEASEGATE_HOST` is omitted, the workflow and Compose file default to `releasegate.maytech.it`.

### VPS prerequisites

Before enabling deployment, the VPS must have:

- Git;
- Docker Engine;
- Docker Compose v2 (`docker compose`);
- the external Docker network `docker_frontend_wp`;
- Traefik attached to that network and configured with the `https` entrypoint and `letsencrypt` certificate resolver;
- enough persistent storage for the PostgreSQL Docker volume;
- SSH access for the configured deployment user.

Verify the Traefik network with:

```bash
docker network inspect docker_frontend_wp
```

The deployment workflow also checks this and fails before Compose startup if the network is missing.

## What the deployment workflow does

After CI succeeds on `main`, the workflow:

1. prepares the SSH key on the GitHub runner;
2. connects to the VPS;
3. creates `/docker/releasegate` when necessary;
4. clones or fetches the private repository using the ephemeral GitHub token;
5. checks out the exact SHA that passed CI;
6. writes the runtime `.env` on the VPS with restrictive file permissions;
7. verifies the Traefik Docker network exists;
8. executes:

```bash
docker compose up -d --build --remove-orphans
```

The GitHub token is used only for the authenticated clone/fetch operation and is not stored as the repository remote URL.

The generated VPS `.env` contains:

```text
POSTGRES_DB=releasegate
POSTGRES_USER=releasegate
POSTGRES_PASSWORD=<repository secret>
DEMO_ACCESS_TOKEN=releasegate-demo-viewer
RELEASEGATE_HOST=<configured host>
```

Only the PostgreSQL password is a privileged secret in this portfolio topology. The demo access token is intentionally browser-visible and read-only.

## Manual VPS deployment

The same root Compose file can be started manually on a compatible VPS.

Create a `.env` file in the repository root:

```text
POSTGRES_DB=releasegate
POSTGRES_USER=releasegate
POSTGRES_PASSWORD=<strong-password>
DEMO_ACCESS_TOKEN=releasegate-demo-viewer
RELEASEGATE_HOST=releasegate.maytech.it
```

Then run:

```bash
docker compose config
docker compose up -d --build
```

Inspect status and logs with:

```bash
docker compose ps
docker compose logs -f app
docker compose logs -f postgres
```

The public health endpoint is:

```text
https://<RELEASEGATE_HOST>/health
```

## Persistence

The VPS stack stores PostgreSQL data in the named volume:

```text
releasegate-postgres
```

Normal container rebuilds and `docker compose down` do not delete this volume.

Do not use `docker compose down -v` on a persistent deployment unless the database is intentionally being destroyed.

### PostgreSQL password rotation

The official PostgreSQL image uses `POSTGRES_PASSWORD` when the data directory is initialized for the first time. Changing the environment value later does not automatically change the password of an existing PostgreSQL role.

For a persistent deployment:

1. rotate the role password inside PostgreSQL;
2. update `RELEASEGATE_POSTGRES_PASSWORD` in GitHub;
3. redeploy the app.

Do not delete the database volume merely to rotate credentials.

## Database migrations

The ASP.NET Core application applies committed EF Core migrations during startup.

The pre-v0.7 compatibility bootstrap is development-only and is not used by VPS or production-like deployments.

Startup migrations are appropriate for the current single-instance topology. If ReleaseGate later runs multiple API replicas, migration execution should move into a dedicated release step.

## Rollback

The normal automated workflow deploys the exact `main` SHA that passed CI.

For an emergency manual rollback on the VPS:

```bash
cd /docker/releasegate
git checkout --detach <known-good-sha>
docker compose up -d --build --remove-orphans
```

Because PostgreSQL migrations may be forward-only, application rollback and database rollback are separate concerns. Do not automatically downgrade the database schema unless the migration has an explicitly tested rollback path.

## Production-like integration stack

`docker-compose.prod.yml` remains separate from the VPS portfolio stack.

It starts:

```text
Browser / SDK
     |
     v
nginx / React :80
     |
     v
ASP.NET Core API :8080
     |
     v
PostgreSQL :5432
```

This topology uses explicit operator, reviewer and runtime credentials and is useful for CI plus manual full-auth testing.

Create its environment file from the template:

```bash
cp .env.production.example .env.production
```

Replace every placeholder value, then start it with:

```bash
docker compose \
  --env-file .env.production \
  -f docker-compose.prod.yml \
  up -d --build
```

The control plane is exposed on `http://localhost:8080` by default.

Stop it while keeping its database volume:

```bash
docker compose \
  --env-file .env.production \
  -f docker-compose.prod.yml \
  down
```

## CI verification

Pull-request CI validates both deployment boundaries.

The production-like job:

1. builds PostgreSQL, API and nginx/web containers;
2. waits for `/health` through nginx;
3. performs authenticated project/flag mutations;
4. verifies runtime snapshot delivery;
5. verifies runtime authentication rejection without a key.

The public-demo job additionally:

1. validates the root VPS Compose configuration;
2. builds the combined root `Dockerfile`;
3. starts it against PostgreSQL;
4. verifies the React SPA is served;
5. verifies demo authentication;
6. verifies seeded data exists;
7. verifies the demo principal receives `403` on write operations.

## Runtime access

The public portfolio demo is intended for human control-plane browsing and does not publish a privileged runtime API key.

A full ReleaseGate deployment may configure runtime credentials and consumers can then call:

```bash
curl \
  -H "X-ReleaseGate-Key: <runtime-key>" \
  "https://<host>/api/runtime/projects/<project>/environments/production/snapshot?subjectKey=<subject>"
```

Runtime credentials remain separate from control-plane bearer tokens.

## SDK publishing

The JavaScript SDK has an independent package lifecycle.

Tags use:

```text
sdk-js-v0.1.0
```

`.github/workflows/sdk-release.yml` verifies that the tag matches `packages/sdk-js/package.json`, runs type checking, tests and package validation, and then publishes with npm provenance when an `NPM_TOKEN` with the required package permission is configured.

The application deployment does not depend on publishing the SDK.
