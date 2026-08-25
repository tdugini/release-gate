# ReleaseGate architecture

## Why this project exists

ReleaseGate is intentionally modeled as an internal developer platform rather than a generic CRUD application. The domain is small enough to understand quickly, but it creates room for real engineering concerns: environment isolation, rollout evaluation, authorization, auditability, concurrency and SDK design.

## v0.1 boundaries

The first milestone contains two deployable applications:

- `ReleaseGate.Api` owns the domain model and persistence;
- `apps/web` is the operator control plane.

PostgreSQL is the system of record.

## Flag identity vs environment state

A feature flag is defined once at project level. Its environment configuration is stored separately in `FeatureFlagEnvironment`.

That distinction matters because `new-checkout` should remain the same flag while moving from development to staging to production. Duplicating one row per environment would make lifecycle operations and future audit history unnecessarily ambiguous.

## API shape

Routes are project-scoped:

```text
/api/projects
/api/projects/{projectKey}
/api/projects/{projectKey}/flags
/api/projects/{projectKey}/flags/{flagKey}
/api/projects/{projectKey}/flags/{flagKey}/environments/{environmentKey}
```

Human-readable keys are used at the HTTP boundary. Internal relationships use UUIDs.

## Persistence

v0.1 uses Entity Framework Core with the Npgsql PostgreSQL provider.

During local development the schema is created automatically to keep the initial bootstrap small. Before the first hosted deployment this will move to explicit EF migrations.

## What is deliberately not in v0.1

Authentication, targeting rules, audit logs, approval workflows and SDK evaluation are not stubbed as fake UI. They will be introduced as complete vertical slices in later milestones.

This keeps the codebase inspectable: a visible feature should have a real data model and behavior behind it.
