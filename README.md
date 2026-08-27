# ReleaseGate

ReleaseGate is a self-hosted feature flag and rollout management platform built as a production-oriented full-stack portfolio project.

It is designed around a real internal-platform problem: teams need a safe way to create flags, separate configuration by environment, progressively expose releases, evaluate flags for individual subjects, understand what changed before production traffic is affected, and deliver those decisions efficiently to applications.

**ASP.NET Core · React · TypeScript · PostgreSQL · Entity Framework Core · Docker**

## Current milestone — v0.5

**SDK & runtime updates — in progress.**

The current milestone adds an application-facing runtime boundary on top of deterministic evaluation and production-safe flag management.

ReleaseGate now provides:

- a subject-specific runtime snapshot containing evaluated flag values;
- a dependency-free JavaScript/TypeScript SDK;
- in-memory `isEnabled()` checks after one snapshot fetch;
- manual and automatic snapshot refresh;
- resilient polling that preserves the last valid configuration on failures;
- conditional HTTP revalidation with ETags and `304 Not Modified` responses.

The runtime API intentionally returns only the final evaluated boolean for each flag. Rollout percentages, buckets, pending changes and audit metadata remain part of the operator-facing control plane.

### Milestone progress

- **v0.1 — Core model:** projects, environments, flags, REST API, PostgreSQL, React control plane and CI.
- **v0.2 — Flag management UI:** project/flag creation, per-environment state and rollout management, validation and operator feedback.
- **v0.3 — Runtime evaluation:** deterministic subject bucketing and percentage rollout evaluation endpoint.
- **v0.4 — Audit & approvals:** persisted change history, control-plane audit view and approval/rejection workflow for production changes.
- **v0.5 — SDK & updates:** runtime snapshot delivery, JavaScript/TypeScript SDK, automatic refresh and conditional revalidation. **Current.**
- **v1.0 — Product polish:** documentation, deployment and portfolio-ready demo.

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

### 3. Start the web app

```bash
cd apps/web
npm ci
npm run dev
```

The web app listens on `http://localhost:5173`.

### 4. Validate the JavaScript SDK

```bash
cd packages/sdk-js
npm ci
npm run typecheck
npm test
```

> The project currently uses `EnsureCreated` during local development. When a milestone introduces a new persisted entity, an existing local PostgreSQL volume may need to be recreated. Explicit EF migrations are planned before hosted deployment.

## API examples

Create a project:

```http
POST /api/projects
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
Content-Type: application/json
X-ReleaseGate-Actor: tommaso

{
  "enabled": true,
  "rolloutPercentage": 25
}
```

Inspect flag change history:

```http
GET /api/projects/silva-commerce/flags/new-checkout/changes?environment=production
```

Approve a pending production change:

```http
POST /api/projects/silva-commerce/flags/new-checkout/changes/{changeId}/approve
X-ReleaseGate-Actor: reviewer
```

Or reject it without changing the active production configuration:

```http
POST /api/projects/silva-commerce/flags/new-checkout/changes/{changeId}/reject
X-ReleaseGate-Actor: reviewer
```

Evaluate one flag for one subject:

```http
POST /api/projects/silva-commerce/flags/new-checkout/evaluate
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

- explicit control-plane and runtime boundaries;
- environment-safe configuration;
- deterministic percentage rollout evaluation;
- efficient snapshot-based SDK consumption;
- resilient runtime configuration refresh;
- accessibility and responsive behavior in the control plane;
- deterministic builds and integration tests;
- auditable production configuration changes;
- approval workflows before sensitive production changes are applied.

Authentication/RBAC, explicit EF migrations, SDK publishing/versioning and production deployment remain future hardening steps.

See `ARCHITECTURE.md` for the current decisions and planned boundaries.
