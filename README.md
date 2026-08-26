# ReleaseGate

ReleaseGate is a self-hosted feature flag and rollout management platform built as a production-oriented full-stack portfolio project.

It is designed around a real internal-platform problem: teams need a safe way to create flags, separate configuration by environment, progressively expose releases, evaluate flags for individual subjects, and understand what changed before production traffic is affected.

**ASP.NET Core · React · TypeScript · PostgreSQL · Entity Framework Core · Docker**

## Current milestone — v0.4

**Audit history & production approvals — ready for verification.**

The current milestone adds traceability and a review workflow on top of the runtime evaluation introduced in v0.3.

ReleaseGate now records and displays feature flag environment changes with:

- previous enabled state and rollout percentage;
- requested enabled state and rollout percentage;
- environment;
- actor;
- timestamp;
- change status;
- reviewer and review timestamp when applicable.

Non-production changes are still applied immediately and recorded as `applied`. Production changes are instead stored as `pending` requests without changing live configuration. An operator can then approve the request, applying the requested production state, or reject it while leaving the current production configuration untouched.

Only one pending production change is allowed per flag at a time, and an already reviewed change cannot be reviewed again. The control plane surfaces the pending state directly on the production environment card and exposes approve/reject actions in the audit history.

### Milestone progress

- **v0.1 — Core model:** projects, environments, flags, REST API, PostgreSQL, React control plane and CI.
- **v0.2 — Flag management UI:** project/flag creation, per-environment state and rollout management, validation and operator feedback.
- **v0.3 — Runtime evaluation:** deterministic subject bucketing and percentage rollout evaluation endpoint.
- **v0.4 — Audit & approvals:** persisted change history, control-plane audit view and approval/rejection workflow for production changes. **Current.**
- **v0.5 — SDK & updates:** application-facing SDK integration and runtime configuration delivery.
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

From v0.4 onward, environment configuration changes also produce persisted history entries so operators can inspect how a flag reached its current state.

## Repository layout

```text
apps/
├── api/ReleaseGate.Api/      ASP.NET Core API
└── web/                      React + TypeScript control plane

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

Evaluate the flag for one subject:

```http
POST /api/projects/silva-commerce/flags/new-checkout/evaluate
Content-Type: application/json

{
  "environment": "production",
  "subjectKey": "user-92841"
}
```

## Engineering direction

ReleaseGate deliberately evolves through complete vertical slices instead of generating a broad admin dashboard full of placeholder features.

The project is built around several constraints:

- explicit domain boundaries;
- environment-safe configuration;
- deterministic percentage rollout evaluation;
- small APIs that can later support SDK consumers;
- accessibility and responsive behavior in the control plane;
- deterministic builds and integration tests;
- auditable production configuration changes;
- approval workflows before sensitive production changes are applied.

Authentication/RBAC, explicit EF migrations, SDK delivery and real-time configuration updates remain future hardening steps.

See `ARCHITECTURE.md` for the current decisions and planned boundaries.
