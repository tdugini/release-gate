# ReleaseGate architecture

## Purpose

ReleaseGate is modeled as an internal developer platform rather than a generic CRUD dashboard. The domain is intentionally compact, but it exercises real engineering concerns: environment isolation, deterministic rollout evaluation, auditability, production approvals, runtime delivery, SDK design, authentication, authorization, schema evolution and deployment.

v1.0 is the completed portfolio baseline. Product polish and public-demo packaging sit on top of the same domain and authorization model rather than bypassing it.

## System boundaries

ReleaseGate has three primary application boundaries:

- `ReleaseGate.Api` — domain model, persistence, control-plane API, runtime API and production migration startup;
- `apps/web` — React control plane for projects, feature flags, environment configuration, approvals and audit history;
- `packages/sdk-js` — JavaScript/TypeScript runtime client that consumes evaluated snapshots.

PostgreSQL is the system of record.

The control plane and runtime API deliberately expose different representations and use different credentials. Human users need configuration, audit and review context; applications only need evaluated feature-flag decisions.

## Domain model

A feature flag is defined once at project level. Environment-specific runtime state is stored separately.

```text
Project
├── ProjectEnvironment
│   ├── development
│   ├── staging
│   └── production
└── FeatureFlag
    └── FeatureFlagEnvironment
        ├── enabled
        ├── rollout percentage
        └── updated timestamp
```

That distinction keeps `new-checkout` as one stable flag while it progresses from development to staging and production.

Environment changes create `FlagChange` records. Non-production changes are applied immediately. Production changes are persisted as pending requests and affect active configuration only after an authorized reviewer approves them.

## Control-plane API

Human-readable project and flag keys are used at the HTTP boundary, while internal relationships use UUIDs.

Representative routes include:

```text
/api/auth/me
/api/projects
/api/projects/{projectKey}
/api/projects/{projectKey}/flags
/api/projects/{projectKey}/flags/{flagKey}
/api/projects/{projectKey}/flags/{flagKey}/changes
/api/projects/{projectKey}/flags/{flagKey}/change-history
/api/projects/{projectKey}/flags/{flagKey}/environments/{environmentKey}
```

Project and flag metadata support create, update and delete operations. The change-history endpoint is paginated and supports environment filtering so audit history can scale independently from the flag detail payload.

## Authentication and authorization

ReleaseGate treats control-plane access and runtime access as separate trust boundaries.

### Control plane

Control-plane routes require bearer authentication. The current implementation uses server-configured static principals so the authorization model remains independent from a specific external identity provider.

Each authenticated identity has:

- a stable subject used for authorization and audit attribution;
- a display name shown in the UI;
- zero or more roles.

The authorization model is:

| Identity | Permissions |
| --- | --- |
| authenticated user with no roles | Read control-plane state and audit history. |
| `operator` | Create/update/delete projects and flags; change environment configuration; submit production changes. |
| `reviewer` | Approve or reject pending production changes created by another identity. |

The API is the authorization source of truth. The React application hides or disables actions for usability, but server-side policies protect every mutation.

Production review enforces separation of duties: the same subject that requested a production change cannot approve or reject it.

### Portfolio demo principal

The public portfolio deployment configures a dedicated authenticated principal with no `operator` or `reviewer` role.

The browser can therefore receive a known demo bearer token and open the product without manual login, while mutation and approval endpoints still return `403 Forbidden`.

This is intentionally different from exposing a privileged credential in the frontend. The demo credential only grants authenticated read access.

### Runtime access

Runtime snapshots use a separate machine-to-machine credential supplied through:

```http
X-ReleaseGate-Key: <runtime-api-key>
```

Runtime credentials contain:

- an API key;
- a client identifier;
- allowed project keys, with `*` supported for wildcard access.

The validator uses constant-time comparison for API keys. Missing or unknown keys return `401 Unauthorized`; a valid key outside its project scope returns `403 Forbidden`.

A control-plane bearer token does not grant runtime access, and a runtime key does not grant control-plane access.

## Deterministic rollout evaluation

Percentage rollout evaluation is deterministic for:

```text
project + flag + environment + subject
```

ReleaseGate hashes that stable identity into a fixed bucket space. A subject therefore remains consistently inside or outside a percentage rollout for a given configuration instead of being randomly re-evaluated on each request.

## Runtime snapshot delivery

Applications consume evaluated snapshots through:

```text
GET /api/runtime/projects/{projectKey}/environments/{environmentKey}/snapshot?subjectKey={subjectKey}
X-ReleaseGate-Key: <runtime-api-key>
```

The response contains only evaluated flag keys and boolean values. Rollout percentages, pending changes and audit metadata stay behind the control-plane boundary.

Snapshots include a weak ETag derived from the evaluated configuration. Consumers can send `If-None-Match`; unchanged snapshots return `304 Not Modified` without another JSON payload.

Responses are marked `private, no-cache`, allowing clients to retain a representation while requiring revalidation instead of permitting shared caches to serve subject-specific configuration.

## JavaScript SDK

`@releasegate/sdk-js` initializes by downloading one runtime snapshot for a subject and stores the evaluated flag map in memory.

`isEnabled(flagKey)` is a local lookup and does not perform a network request.

Manual `refresh()` and optional automatic polling revalidate with the current ETag. A `304` keeps the current in-memory snapshot; a changed response atomically replaces it.

Polling schedules the next request only after the previous one completes, preventing overlapping refreshes. Failed refreshes preserve the last valid snapshot.

### SDK release boundary

The SDK package version is independent from the ReleaseGate application version.

Tags use:

```text
sdk-js-v<package-version>
```

The release workflow requires the tag version to match `packages/sdk-js/package.json`, then runs type checking, tests and package validation before npm publication. Publishing requires an `NPM_TOKEN` with permission for the package scope.

## Persistence and migrations

ReleaseGate uses Entity Framework Core with Npgsql and PostgreSQL.

Schema evolution is managed through committed EF Core migrations. The repository also pins `dotnet-ef` through the local tool manifest.

For relational databases the API applies committed migrations during startup.

Development mode additionally:

1. runs the narrow compatibility bootstrap for pre-v0.7 local databases;
2. applies migrations;
3. seeds development data.

Portfolio demo mode applies normal migrations and seeds the same representative dataset only when the database is empty. The legacy compatibility bootstrap remains development-only.

For the current single-instance deployment, startup migration keeps installation simple. If multiple API replicas are introduced later, schema migration should move to a dedicated release job.

## Deployment architecture

ReleaseGate has two deliberate deployment topologies.

### VPS portfolio topology

The root `Dockerfile` produces one application image.

Build stages:

```text
React source
   |
   v
Vite build
   |
   v
ASP.NET wwwroot
   |
   v
ASP.NET publish
   |
   v
single runtime image :8080
```

The root `docker-compose.yml` deploys that image with PostgreSQL:

```text
Internet
   |
   v
Traefik
   |
   v
ReleaseGate app :8080
React SPA + ASP.NET Core
   |
   v
PostgreSQL :5432
```

The app joins two networks:

- `releasegate-internal` — private application/database communication;
- `docker_frontend_wp` — existing external Traefik network.

PostgreSQL joins only `releasegate-internal` and publishes no host port.

Traefik discovers the app through Compose labels, terminates TLS and routes the configured hostname to container port `8080`.

Demo mode enables static SPA serving, SPA fallback routing, seed data and the read-only portfolio principal.

### Production-like integration topology

`docker-compose.prod.yml` intentionally keeps the earlier three-container topology:

```text
client
  |
  v
nginx / React
  |
  v
ReleaseGate.Api
  |
  v
PostgreSQL
```

This stack is useful for integration verification and full-auth self-hosted testing. nginx serves the SPA and proxies same-origin `/api/*` and `/health` requests to the API.

Keeping this topology in CI means the original separate API/web production images remain continuously validated even though the portfolio VPS uses the simpler combined image.

## CI/CD boundary

Pull-request CI validates application behavior and deployability before anything reaches the VPS.

The current pipeline covers:

- .NET restore, build and integration tests;
- EF model/migration consistency;
- applying migrations to an empty PostgreSQL database;
- React install, typecheck and build;
- SDK typecheck, tests and package validation;
- the production-like nginx/API/PostgreSQL Compose stack;
- the combined portfolio image;
- the root VPS Compose configuration;
- seeded portfolio data;
- read-only demo authorization, including `403` on attempted mutations.

`.github/workflows/deploy-vps.yml` is triggered by completion of the `CI` workflow on `main`, not directly by a push.

Deployment proceeds only when:

```text
CI conclusion == success
AND
VPS_DEPLOY_ENABLED == true
```

The deployment workflow sends the exact tested `head_sha` to the VPS, checks out that revision and runs:

```bash
docker compose up -d --build --remove-orphans
```

This prevents the deployment job from silently building a different revision than the one validated by CI.

## Current hardening boundary

The v1.0 portfolio baseline includes authentication, RBAC, audit history, approval separation of duties, runtime API credentials, schema migrations, SDK packaging, production-like integration testing and automated single-host deployment.

Potential post-v1.0 hardening includes:

- OIDC/OAuth identity-provider integration;
- external secret-manager integration and credential rotation;
- dedicated migration orchestration for multiple replicas;
- horizontal runtime scaling;
- push or streaming configuration delivery only if polling becomes a real constraint.

Those are intentional future boundaries, not missing prerequisites for the v1.0 portfolio release.
