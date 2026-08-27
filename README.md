# ReleaseGate

ReleaseGate is a self-hosted feature flag and rollout management platform built as a production-oriented full-stack portfolio project.

It is designed around a real internal-platform problem: teams need a safe way to create flags, separate configuration by environment, progressively expose releases, evaluate flags for individual subjects, understand what changed before production traffic is affected, and deliver those decisions efficiently to applications.

**ASP.NET Core · React · TypeScript · PostgreSQL · Entity Framework Core · Docker**

## Current milestone — v0.7

**Versioned database migrations.**

ReleaseGate now manages PostgreSQL schema evolution through explicit EF Core migrations instead of relying on `EnsureCreated` during local development.

ReleaseGate now provides:

- an initial migration for the existing ReleaseGate schema;
- a versioned EF Core model snapshot;
- a repository-pinned `dotnet-ef` CLI tool;
- automatic `MigrateAsync()` startup in local development;
- safe baselining of complete legacy databases created before v0.7;
- refusal to auto-baseline partial or ambiguous legacy schemas;
- CI verification that the EF model snapshot is current;
- CI application of migrations against a real empty PostgreSQL database.

The legacy upgrade path was also verified manually by creating a database with the v0.6 `EnsureCreated` bootstrap, switching to v0.7 without deleting the PostgreSQL volume, and confirming that the existing projects and flags remained available while the initial migration was recorded in `__EFMigrationsHistory`.

### Milestone progress

- **v0.1 — Core model:** projects, environments, flags, REST API, PostgreSQL, React control plane and CI.
- **v0.2 — Flag management UI:** project/flag creation, per-environment state and rollout management, validation and operator feedback.
- **v0.3 — Runtime evaluation:** deterministic subject bucketing and percentage rollout evaluation endpoint.
- **v0.4 — Audit & approvals:** persisted change history, control-plane audit view and approval/rejection workflow for production changes.
- **v0.5 — SDK & updates:** runtime snapshot delivery, JavaScript/TypeScript SDK, automatic refresh and conditional revalidation.
- **v0.6 — Authentication & RBAC:** authenticated control plane, operator/reviewer roles, authenticated audit actors and self-review protection.
- **v0.7 — EF Core migrations:** versioned schema evolution, legacy database baselining and migration verification in CI. **Current.**
- **v0.8 — Runtime security:** production-grade machine-to-machine access for runtime configuration delivery.
- **v0.9 — SDK & deployment:** SDK publishing/versioning and production-like deployment.
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

## Control-plane roles

ReleaseGate currently defines two control-plane roles:

| Role | Capabilities |
| --- | --- |
| `operator` | Create projects and flags, and change environment configuration. Production mutations are submitted for review instead of being applied immediately. |
| `reviewer` | Inspect control-plane state and approve or reject pending production changes created by another identity. |

A reviewer cannot approve or reject their own production change.

## Repository layout

```text
apps/
├── api/ReleaseGate.Api/      ASP.NET Core API
└── web/                      React + TypeScript control plane

packages/
└── sdk-js/                   JavaScript/TypeScript runtime SDK

tests/
└── ReleaseGate.Api.IntegrationTests/

.github/workflows/
└── ci.yml

docker-compose.yml             local PostgreSQL
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

Development control-plane identities are configured in `apps/api/ReleaseGate.Api/appsettings.Development.json`:

- operator token: `releasegate-local-operator`
- reviewer token: `releasegate-local-reviewer`

These values are development-only credentials and are intentionally stored in development configuration for local testing.

### 3. Start the web app

```bash
cd apps/web
npm ci
npm run dev
```

The web app listens on `http://localhost:5173`. Enter one of the development bearer tokens on the access screen to open the control plane.

### 4. Validate the JavaScript SDK

```bash
cd packages/sdk-js
npm ci
npm run typecheck
npm test
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

Create a flag:

```http
POST /api/projects/silva-commerce/flags
Authorization: Bearer releasegate-local-operator
Content-Type: application/json

{
  "name": "New checkout",
  "key": "new-checkout",
  "description": "Progressive rollout of the redesigned checkout"
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

Inspect flag change history:

```http
GET /api/projects/silva-commerce/flags/new-checkout/changes?environment=production
Authorization: Bearer releasegate-local-reviewer
```

Approve a pending production change as a different reviewer identity:

```http
POST /api/projects/silva-commerce/flags/new-checkout/changes/{changeId}/approve
Authorization: Bearer releasegate-local-reviewer
```

Or reject it without changing the active production configuration:

```http
POST /api/projects/silva-commerce/flags/new-checkout/changes/{changeId}/reject
Authorization: Bearer releasegate-local-reviewer
```

Evaluate one flag for one subject through the authenticated control-plane inspection endpoint:

```http
POST /api/projects/silva-commerce/flags/new-checkout/evaluate
Authorization: Bearer releasegate-local-operator
Content-Type: application/json

{
  "environment": "production",
  "subjectKey": "user-92841"
}
```

Fetch the application-facing runtime snapshot for that subject:

```http
GET /api/runtime/projects/silva-commerce/environments/production/snapshot?subjectKey=user-92841
```

The runtime snapshot endpoint does not use control-plane bearer authentication. It represents the application-consumption boundary and is intentionally kept separate from administrative permissions.

Runtime responses include an ETag. Consumers can revalidate the cached snapshot without downloading an unchanged JSON payload:

```http
GET /api/runtime/projects/silva-commerce/environments/production/snapshot?subjectKey=user-92841
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
  refreshInterval: 30_000,
});

await client.initialize('user-92841');

if (client.isEnabled('new-checkout')) {
  // expose the new release path
}
```

After initialization, flag checks are local in-memory lookups. Refreshes revalidate the current snapshot through ETags, and automatic polling never overlaps requests.

## Engineering direction

ReleaseGate deliberately evolves through complete vertical slices instead of generating a broad admin dashboard full of placeholder features.

The project is built around several constraints:

- explicit authenticated control-plane and application-facing runtime boundaries;
- role-based authorization for management and production review operations;
- versioned and repeatable database schema evolution;
- environment-safe configuration;
- deterministic percentage rollout evaluation;
- efficient snapshot-based SDK consumption;
- resilient runtime configuration refresh;
- accessibility and responsive behavior in the control plane;
- deterministic builds and integration tests;
- auditable production configuration changes with authenticated actors;
- separation of duties for sensitive production changes.

Production-grade runtime credentials, real identity-provider integration, SDK publishing/versioning and production deployment remain future hardening steps.

See `ARCHITECTURE.md` for the current decisions and planned boundaries.
