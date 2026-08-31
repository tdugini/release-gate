# ReleaseGate

ReleaseGate is a self-hosted feature flag and progressive-delivery platform built as a production-oriented full-stack application.

It models a real internal-platform problem: teams need a safe way to create feature flags, isolate configuration by environment, roll changes out gradually, evaluate flags for individual subjects, review production changes, inspect audit history and deliver runtime decisions efficiently to applications.

**ASP.NET Core · React · TypeScript · PostgreSQL · Entity Framework Core · Docker · Traefik · GitHub Actions**

## Status — v1.0

ReleaseGate v1.0 is feature-complete.

The project includes:

- project CRUD and feature-flag CRUD;
- development, staging and production environments;
- per-environment enable/disable state and percentage rollouts;
- deterministic subject bucketing;
- production approval/rejection workflow with separation of duties;
- persisted and paginated change history;
- authenticated control plane with operator/reviewer RBAC;
- separate machine-to-machine runtime API keys;
- snapshot delivery with ETag revalidation;
- JavaScript/TypeScript runtime SDK;
- EF Core migrations and PostgreSQL persistence;
- production-like container smoke tests in CI;
- a polished responsive React control plane;
- a read-only demo mode;
- automated VPS deployment behind Traefik after successful CI.

### Milestones

- **v0.1 — Core model:** projects, environments, flags, REST API, PostgreSQL, React control plane and CI.
- **v0.2 — Flag management:** project/flag creation, environment state, rollouts and validation.
- **v0.3 — Runtime evaluation:** deterministic subject bucketing and percentage rollout evaluation.
- **v0.4 — Audit & approvals:** persisted change history and production approval/rejection workflow.
- **v0.5 — SDK & updates:** runtime snapshots, JavaScript SDK, refresh and conditional revalidation.
- **v0.6 — Authentication & RBAC:** operator/reviewer roles, authenticated audit actors and self-review protection.
- **v0.7 — EF Core migrations:** versioned schema evolution and migration verification in CI.
- **v0.8 — Runtime security:** machine-to-machine API keys and project scopes.
- **v0.9 — SDK & deployment:** versioned SDK release pipeline and production-like deployment verification.
- **v1.0 — Product release:** CRUD completion, paginated history, visual polish and public deployment path. **Current.**

## Live demo

https://releasegate.maytech.it

The public demo runs in read-only mode so projects, feature flags, environments and change history can be explored without allowing mutation or approval operations.

The demo environment automatically authenticates a dedicated read-only principal. It has no `operator` or `reviewer` roles, so mutation and approval endpoints remain forbidden by the API.

The browser-visible demo token is intentionally read-only. It is not a runtime SDK credential and does not grant access to privileged control-plane operations.

## Product model

A feature flag belongs to a project, while its runtime state belongs to an environment.

```text
Project
├── Environment: development
├── Environment: staging
├── Environment: production
└── FeatureFlag
    ├── development setting
    ├── staging setting
    └── production setting
```

The flag identity stays stable throughout its lifecycle instead of being duplicated per environment.

Non-production changes are applied immediately and recorded in history. Production changes are persisted as pending requests and only affect runtime traffic after approval by a different authorized identity.

## Security boundaries

ReleaseGate separates human control-plane access from application runtime access.

### Control plane

Control-plane requests use bearer authentication.

| Identity | Capabilities |
| --- | --- |
| authenticated read-only user | Inspect projects, flags, environments and history. |
| `operator` | Create/update/delete projects and flags, change environment configuration and submit production changes for review. |
| `reviewer` | Approve or reject pending production changes created by another identity. |

A reviewer cannot approve or reject their own production change.

### Runtime

Applications and SDKs authenticate separately with:

```http
X-ReleaseGate-Key: <runtime-api-key>
```

Runtime keys may be scoped to one or more projects, or to `*` for wildcard access. A control-plane token does not grant runtime access, and a runtime key does not grant control-plane access.

## Runtime delivery

Applications consume an evaluated snapshot for one project, environment and subject:

```http
GET /api/runtime/projects/{projectKey}/environments/{environmentKey}/snapshot?subjectKey={subjectKey}
X-ReleaseGate-Key: <runtime-api-key>
```

Percentage rollout decisions are deterministic for the tuple:

```text
project + flag + environment + subject
```

Responses include an ETag. Consumers can revalidate with `If-None-Match`; unchanged snapshots return `304 Not Modified`.

## Repository layout

```text
apps/
├── api/ReleaseGate.Api/      ASP.NET Core API
└── web/                      React control plane

packages/
└── sdk-js/                   JavaScript/TypeScript runtime SDK

tests/
└── ReleaseGate.Api.IntegrationTests/

.github/workflows/
├── ci.yml                    build, tests and deployment smoke tests
├── deploy-vps.yml            deploy tested main revision to the VPS
└── sdk-release.yml           versioned npm SDK release workflow

Dockerfile                    combined React + ASP.NET application image
docker-compose.yml            VPS deployment stack + Traefik labels
docker-compose.prod.yml       production-like nginx/API/PostgreSQL stack
ARCHITECTURE.md                architecture decisions and boundaries
DEPLOYMENT.md                  deployment and operations guide
```

## Local development

### 1. Start PostgreSQL

Create a local environment file:

```bash
cp .env.example .env
```

Then start only PostgreSQL from the root Compose file:

```bash
docker compose up -d postgres
```

### 2. Start the API

Requires .NET 10 SDK.

```bash
dotnet run --project apps/api/ReleaseGate.Api
```

The development API listens on `http://localhost:5080`.

Development mode automatically applies pending EF Core migrations and seeds sample data. Existing pre-v0.7 local databases with the complete legacy schema are baselined once and then managed through normal migration history.

Development credentials are configured in `apps/api/ReleaseGate.Api/appsettings.Development.json`:

- operator token: `releasegate-local-operator`
- reviewer token: `releasegate-local-reviewer`
- runtime API key: `releasegate-local-runtime`

These are development-only credentials.

### 3. Start the React control plane

```bash
cd apps/web
npm ci
npm run dev
```

The web app listens on `http://localhost:5173` and talks to the development API on port `5080`.

### 4. Validate the JavaScript SDK

```bash
cd packages/sdk-js
npm ci
npm run typecheck
npm test
npm run pack:check
```

### 5. Work with EF Core migrations

```bash
dotnet tool restore

dotnet ef migrations has-pending-model-changes \
  --project apps/api/ReleaseGate.Api

dotnet ef database update \
  --project apps/api/ReleaseGate.Api
```

Persisted model changes should include a committed migration and an updated model snapshot.

## Deployment modes

ReleaseGate intentionally keeps two different Compose topologies.

### VPS deployment

`docker-compose.yml` is the deployable root stack used by the VPS automation.

It runs:

```text
Internet
   |
   v
Traefik
   |
   v
ReleaseGate app :8080
React + ASP.NET Core
   |
   v
PostgreSQL
```

The root `Dockerfile` builds React first, copies the compiled SPA into the ASP.NET application and publishes a single application image. Traefik routes HTTPS traffic to port `8080` through the existing external `docker_frontend_wp` network.

PostgreSQL is attached only to the private `releasegate-internal` network and exposes no host port.

The VPS workflow runs after a successful `CI` workflow on `main`, deploys the exact tested commit and executes:

```bash
docker compose up -d --build --remove-orphans
```

Deployment remains disabled until the repository variable `VPS_DEPLOY_ENABLED` is set to `true`.

See `DEPLOYMENT.md` for the required secrets, variables, DNS and Traefik prerequisites.

### Production-like integration stack

`docker-compose.prod.yml` remains the full-auth production-like stack used for integration verification and manual self-hosted testing.

It packages PostgreSQL, the ASP.NET API and a separate nginx/React container. nginx serves the SPA and proxies same-origin `/api/*` and `/health` requests to the API.

Start it with:

```bash
cp .env.production.example .env.production

docker compose \
  --env-file .env.production \
  -f docker-compose.prod.yml \
  up -d --build
```

The control plane is exposed on `http://localhost:8080` by default.

## API examples

Inspect the current identity:

```http
GET /api/auth/me
Authorization: Bearer releasegate-local-operator
```

Create a project:

```http
POST /api/projects
Authorization: Bearer releasegate-local-operator
Content-Type: application/json

{
  "name": "Silva Commerce",
  "key": "silva-commerce",
  "description": "Checkout and storefront release controls"
}
```

Submit a production rollout change:

```http
PATCH /api/projects/silva-commerce/flags/new-checkout/environments/production
Authorization: Bearer releasegate-local-operator
Content-Type: application/json

{
  "enabled": true,
  "rolloutPercentage": 25
}
```

Approve it as a different reviewer:

```http
POST /api/projects/silva-commerce/flags/new-checkout/changes/{changeId}/approve
Authorization: Bearer releasegate-local-reviewer
```

Read the paginated audit history:

```http
GET /api/projects/silva-commerce/flags/new-checkout/change-history?page=1&pageSize=10
Authorization: Bearer releasegate-local-operator
```

## JavaScript SDK

```ts
import { ReleaseGateClient } from '@releasegate/sdk-js';

const client = new ReleaseGateClient({
  baseUrl: 'http://localhost:5080',
  projectKey: 'silva-commerce',
  environment: 'production',
  apiKey: 'releasegate-local-runtime',
  refreshInterval: 30_000,
});

await client.initialize('user-92841');

if (client.isEnabled('new-checkout')) {
  // expose the new release path
}
```

After initialization, flag checks are local in-memory lookups. Refreshes use ETag revalidation and automatic polling never overlaps requests.

The SDK has an independent version lifecycle. Tags use `sdk-js-v<package-version>` and must match the version in `packages/sdk-js/package.json`.

## CI/CD

Pull-request CI validates:

- .NET restore/build/tests;
- EF migration/model-snapshot consistency;
- applying migrations to an empty PostgreSQL database;
- React install/typecheck/build;
- SDK typecheck/tests/package contents;
- the production-like Compose stack;
- the combined public-demo image;
- the root VPS Compose configuration;
- seeded demo data;
- read-only demo authorization, including `403` on mutation attempts.

After `main` passes CI, `.github/workflows/deploy-vps.yml` may deploy the exact tested SHA when VPS deployment is enabled.

## Engineering boundaries

ReleaseGate v1.0 deliberately stops before adding infrastructure that would not materially improve the current product scope.

Potential future hardening includes:

- OIDC/OAuth identity-provider integration;
- external secret-manager integration and credential rotation;
- migration orchestration for multiple API replicas;
- horizontally scaled runtime delivery;
- push/streaming configuration only if polling becomes a real constraint.

See `ARCHITECTURE.md` for the design decisions and `DEPLOYMENT.md` for operational details.
