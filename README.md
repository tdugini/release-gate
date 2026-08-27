# ReleaseGate

ReleaseGate is a self-hosted feature flag and rollout management platform built as a production-oriented full-stack portfolio project.

It is designed around a real internal-platform problem: teams need a safe way to create flags, separate configuration by environment, progressively expose releases, evaluate flags for individual subjects, understand what changed before production traffic is affected, and deliver those decisions efficiently to applications.

**ASP.NET Core · React · TypeScript · PostgreSQL · Entity Framework Core · Docker**

## Current milestone — v0.9

**SDK releases & production-like deployment.**

ReleaseGate can now be built and exercised as a complete self-hosted stack instead of only as separate local development processes. The JavaScript SDK is also prepared for explicit, versioned package releases.

ReleaseGate now provides:

- multi-stage production container images for the ASP.NET Core API and React control plane;
- nginx serving the SPA and proxying same-origin `/api/*` traffic to the API container;
- a production-like Docker Compose stack with PostgreSQL, API and web services;
- deployment credentials supplied through environment variables rather than committed production secrets;
- automatic EF Core migration application for relational deployed environments;
- a full deployment smoke test in CI that exercises migrations, nginx, control-plane auth and runtime auth together;
- npm package-content validation for `@releasegate/sdk-js`;
- an SDK release workflow driven by `sdk-js-v*` tags, with tag/package version matching and npm provenance.

### Milestone progress

- **v0.1 — Core model:** projects, environments, flags, REST API, PostgreSQL, React control plane and CI.
- **v0.2 — Flag management UI:** project/flag creation, per-environment state and rollout management, validation and operator feedback.
- **v0.3 — Runtime evaluation:** deterministic subject bucketing and percentage rollout evaluation endpoint.
- **v0.4 — Audit & approvals:** persisted change history, control-plane audit view and approval/rejection workflow for production changes.
- **v0.5 — SDK & updates:** runtime snapshot delivery, JavaScript/TypeScript SDK, automatic refresh and conditional revalidation.
- **v0.6 — Authentication & RBAC:** authenticated control plane, operator/reviewer roles, authenticated audit actors and self-review protection.
- **v0.7 — EF Core migrations:** versioned schema evolution, legacy database baselining and migration verification in CI.
- **v0.8 — Runtime security:** machine-to-machine API keys, project scopes and SDK credential propagation.
- **v0.9 — SDK & deployment:** versioned SDK release pipeline and production-like container deployment. **Current.**
- **v1.0 — Product polish:** operational documentation, portfolio-ready demo and final visual overhaul.

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

This avoids duplicating the flag definition for every environment and keeps the identity of a flag stable throughout its lifecycle.

Environment configuration changes also produce persisted history entries so operators can inspect how a flag reached its current state. Production changes must be reviewed before they affect runtime traffic.

## Security boundaries

ReleaseGate intentionally separates human control-plane access from application runtime access.

### Control plane

Control-plane users authenticate with bearer tokens and are assigned roles:

| Role | Capabilities |
| --- | --- |
| `operator` | Create projects and flags, and change environment configuration. Production mutations are submitted for review instead of being applied immediately. |
| `reviewer` | Inspect control-plane state and approve or reject pending production changes created by another identity. |

A reviewer cannot approve or reject their own production change.

### Runtime

Applications and SDKs authenticate separately with `X-ReleaseGate-Key`.

Runtime keys may be scoped to one or more project keys, or to `*` for wildcard access. A control-plane bearer token does not grant runtime access, and a runtime key does not grant control-plane access.

## Repository layout

```text
apps/
├── api/ReleaseGate.Api/      ASP.NET Core API + API Dockerfile
└── web/                      React control plane + nginx container

packages/
└── sdk-js/                   JavaScript/TypeScript runtime SDK

tests/
└── ReleaseGate.Api.IntegrationTests/

.github/workflows/
├── ci.yml                    build, tests and deployed-stack smoke test
└── sdk-release.yml           versioned npm release workflow

docker-compose.yml            local PostgreSQL
docker-compose.prod.yml       production-like self-hosted stack
DEPLOYMENT.md                  deployment and release operations
```

## Local development

### 1. Start PostgreSQL

```bash
docker compose up -d postgres
```

### 2. Start the API

Requires .NET 10 SDK.

```bash
dotnet run --project apps/api/ReleaseGate.Api
```

The development API listens on `http://localhost:5080`.

During development the API applies pending EF Core migrations automatically before seeding development data. Existing pre-v0.7 local databases with the complete legacy schema are baselined once and then managed through normal migration history.

Development credentials are configured in `apps/api/ReleaseGate.Api/appsettings.Development.json`:

- operator token: `releasegate-local-operator`
- reviewer token: `releasegate-local-reviewer`
- runtime API key: `releasegate-local-runtime`

These values are development-only credentials and are intentionally stored in development configuration for local testing.

### 3. Start the web app

```bash
cd apps/web
npm ci
npm run dev
```

The web app listens on `http://localhost:5173`. Enter one of the development control-plane bearer tokens on the access screen to open the control plane.

### 4. Validate the JavaScript SDK

```bash
cd packages/sdk-js
npm ci
npm run typecheck
npm test
npm run pack:check
```

### 5. Work with EF Core migrations

Restore the repository-pinned EF tool:

```bash
dotnet tool restore
```

Check whether the current model requires a migration:

```bash
dotnet ef migrations has-pending-model-changes --project apps/api/ReleaseGate.Api
```

Apply all pending migrations explicitly when needed:

```bash
dotnet ef database update --project apps/api/ReleaseGate.Api
```

New persisted model changes should be accompanied by a committed migration and an updated model snapshot.

## Production-like deployment

Copy the deployment environment template and replace all placeholder secrets:

```bash
cp .env.production.example .env.production
```

Then build and start PostgreSQL, API and web/nginx together:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
```

The control plane is exposed at `http://localhost:8080` by default. Only nginx is published to the host; API and PostgreSQL traffic stay on the internal Compose network.

In production builds, the React application uses same-origin API calls and nginx proxies `/api/*` to the API container. The API applies committed EF Core migrations at startup. Legacy pre-v0.7 baselining remains development-only.

See `DEPLOYMENT.md` for configuration, lifecycle commands, CI smoke-test behavior and SDK release instructions.

## API examples

Control-plane requests require an authenticated bearer token:

```http
Authorization: Bearer releasegate-local-operator
```

Inspect the current control-plane identity:

```http
GET /api/auth/me
Authorization: Bearer releasegate-local-operator
```

Create a project as an operator:

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

Submit a production rollout change for review:

```http
PATCH /api/projects/silva-commerce/flags/new-checkout/environments/production
Authorization: Bearer releasegate-local-operator
Content-Type: application/json

{
  "enabled": true,
  "rolloutPercentage": 25
}
```

The audit actor is derived from the authenticated identity. Callers cannot override it with a request header.

Approve a pending production change as a different reviewer identity:

```http
POST /api/projects/silva-commerce/flags/new-checkout/changes/{changeId}/approve
Authorization: Bearer releasegate-local-reviewer
```

Fetch the application-facing runtime snapshot for a subject with a runtime credential:

```http
GET /api/runtime/projects/silva-commerce/environments/production/snapshot?subjectKey=user-92841
X-ReleaseGate-Key: releasegate-local-runtime
```

The runtime snapshot endpoint does not accept control-plane bearer authentication as a substitute for the runtime key.

Runtime responses include an ETag. Consumers can revalidate the cached snapshot without downloading an unchanged JSON payload:

```http
GET /api/runtime/projects/silva-commerce/environments/production/snapshot?subjectKey=user-92841
X-ReleaseGate-Key: releasegate-local-runtime
If-None-Match: W/"..."
```

When the evaluated configuration has not changed, ReleaseGate responds with `304 Not Modified`.

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

After initialization, flag checks are local in-memory lookups. Refreshes revalidate the current snapshot through ETags, and automatic polling never overlaps requests.

The SDK package has its own version lifecycle. A tag such as `sdk-js-v0.1.0` must match the version in `packages/sdk-js/package.json`; the release workflow validates, tests and packs the SDK before publishing to npm. Actual npm publication requires an `NPM_TOKEN` and publish rights for the package scope.

## Engineering direction

ReleaseGate deliberately evolves through complete vertical slices instead of generating a broad admin dashboard full of placeholder features.

The project is built around several constraints:

- explicit separation between authenticated human control-plane access and machine-to-machine runtime access;
- role-based authorization for management and production review operations;
- project-scoped runtime API credentials;
- versioned and repeatable database schema evolution;
- production-like container packaging and same-origin reverse proxying;
- deployable configuration kept outside committed production secrets;
- deterministic percentage rollout evaluation;
- efficient snapshot-based SDK consumption;
- resilient runtime configuration refresh;
- versioned and validated SDK package releases;
- deterministic builds, integration tests and deployed-stack smoke tests;
- auditable production configuration changes with separation of duties.

Real identity-provider integration, external secret-manager integration, credential rotation and horizontally scaled deployment remain future hardening opportunities rather than prerequisites for the portfolio v1.0.

See `ARCHITECTURE.md` for the current decisions and boundaries, and `DEPLOYMENT.md` for deployment operations.
