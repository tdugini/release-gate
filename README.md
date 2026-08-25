# ReleaseGate

ReleaseGate is a self-hosted feature flag and rollout management platform built as a production-oriented full-stack portfolio project.

It is designed around a real internal-platform problem: teams need a safe way to create flags, separate configuration by environment, inspect rollout state, and understand what is changing before a release reaches production.

**ASP.NET Core · React · TypeScript · PostgreSQL · Entity Framework Core · Docker**

## Current milestone — v0.1

The first milestone establishes the product model and the vertical slice from database to UI:

- projects;
- default `development`, `staging`, and `production` environments;
- boolean feature flags;
- per-environment enabled state;
- percentage rollout;
- project and flag REST endpoints;
- responsive React control plane;
- API contract tests;
- Dockerized PostgreSQL;
- CI for backend and frontend verification.

Later milestones will add authentication/RBAC, targeting rules, audit history, production approvals, SDK evaluation and real-time updates.

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

Change a production rollout:

```http
PATCH /api/projects/silva-commerce/flags/new-checkout/environments/production
Content-Type: application/json

{
  "enabled": true,
  "rolloutPercentage": 25
}
```

## Engineering direction

ReleaseGate deliberately starts with a narrow vertical slice instead of generating a broad admin dashboard full of placeholder features.

The project will evolve around several constraints:

- explicit domain boundaries;
- environment-safe configuration;
- small APIs that can later support SDK consumers;
- accessibility and responsive behavior in the control plane;
- deterministic builds and integration tests;
- production changes that become auditable and approvable in later milestones.

See `ARCHITECTURE.md` for the current decisions and planned boundaries.
