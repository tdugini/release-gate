# ReleaseGate architecture

## Why this project exists

ReleaseGate is intentionally modeled as an internal developer platform rather than a generic CRUD application. The domain is small enough to understand quickly, while still creating room for real engineering concerns: environment isolation, rollout evaluation, auditability, approvals, runtime delivery, SDK design, authentication and authorization.

## System boundaries

ReleaseGate currently contains three main boundaries:

- `ReleaseGate.Api` owns the domain model, persistence, authenticated control-plane HTTP API and runtime delivery API;
- `apps/web` is the authenticated operator control plane used to manage projects, flags, environment configuration and production reviews;
- `packages/sdk-js` is the application-facing JavaScript/TypeScript SDK used by consumers to read evaluated feature flags.

PostgreSQL is the system of record.

The control plane and runtime API deliberately expose different representations and security concerns. Operators need configuration, rollout, audit and review detail. Applications only need the final evaluated value of each flag for their current subject.

## Authentication and authorization boundary

Control-plane routes require bearer authentication. The current implementation uses server-configured static tokens because v0.6 is focused on the authorization model and control-plane boundary rather than introducing a placeholder external identity provider.

An authenticated control-plane identity contains:

- a stable subject used for authorization and audit attribution;
- a display name used by the React control plane;
- one or more roles.

The current roles are:

- `operator` — may create projects and flags and mutate environment configuration;
- `reviewer` — may approve or reject pending production changes created by another identity.

Read-only control-plane access requires authentication, while mutation endpoints enforce the appropriate role. Audit actors are derived from the authenticated subject instead of caller-provided headers, which prevents clients from spoofing who requested or reviewed a change.

Production review also enforces separation of duties: the identity that requested a pending production change cannot approve or reject that same change.

The React control plane resolves the current identity through `/api/auth/me`, persists the development access token locally, exposes assigned roles in the shell, and removes or disables actions the current role cannot perform. The API remains the source of truth for authorization; UI gating is only a usability layer.

The runtime snapshot endpoint is intentionally outside control-plane authentication. It is an application-consumption boundary and will eventually need its own production-grade machine-to-machine access strategy rather than inheriting human control-plane roles.

## Flag identity vs environment state

A feature flag is defined once at project level. Its environment configuration is stored separately in `FeatureFlagEnvironment`.

That distinction matters because `new-checkout` should remain the same flag while moving from development to staging to production. Duplicating one flag per environment would make lifecycle operations, evaluation and audit history unnecessarily ambiguous.

## Control-plane API

Control-plane routes are project-scoped:

```text
/api/auth/me
/api/projects
/api/projects/{projectKey}
/api/projects/{projectKey}/flags
/api/projects/{projectKey}/flags/{flagKey}
/api/projects/{projectKey}/flags/{flagKey}/changes
/api/projects/{projectKey}/flags/{flagKey}/environments/{environmentKey}
```

Human-readable keys are used at the HTTP boundary. Internal relationships use UUIDs.

Non-production environment changes are applied immediately and recorded in the audit history. Production changes are persisted as pending requests and only affect active runtime configuration after approval. Rejection leaves the current production state unchanged.

## Runtime evaluation

Percentage rollout evaluation is deterministic for the tuple:

```text
project + flag + environment + subject
```

ReleaseGate hashes that stable identity into a fixed bucket space. The same subject therefore receives the same decision for a given rollout configuration instead of randomly moving in and out of a rollout between requests.

The single-flag evaluation endpoint remains useful for authenticated control-plane inspection and testing, while application consumers use a runtime snapshot endpoint:

```text
GET /api/runtime/projects/{projectKey}/environments/{environmentKey}/snapshot?subjectKey={subjectKey}
```

The runtime snapshot contains only evaluated flag keys and boolean values. Rollout percentages, buckets, audit metadata and pending production changes stay behind the control-plane boundary.

## Runtime configuration delivery

The runtime snapshot is designed to support many local flag checks from one network request.

Responses include a weak HTTP ETag derived from the evaluated flag set for the project, environment and subject. The ETag is weak because response metadata such as `generatedAt` can change even when the meaningful evaluated configuration has not.

Consumers can revalidate with `If-None-Match`. When the evaluated configuration is unchanged, the API returns `304 Not Modified` without another JSON payload. Runtime responses are marked `private, no-cache` so clients may retain the representation but must revalidate it rather than allowing shared caches to serve subject-specific configuration.

## JavaScript SDK

`@releasegate/sdk-js` initializes by downloading one runtime snapshot for a subject and stores the evaluated flags in memory.

`isEnabled(flagKey)` is a local lookup and does not make a network request. Manual `refresh()` and optional automatic polling revalidate the snapshot using its ETag. A `304` keeps the current in-memory snapshot, while a changed response atomically replaces the flag map.

Automatic polling schedules the next refresh only after the previous request finishes, preventing overlapping requests when the runtime API is slow. Failed refreshes preserve the last valid snapshot and can be surfaced through the consumer error callback without stopping the refresh loop.

## Persistence

ReleaseGate uses Entity Framework Core with the Npgsql PostgreSQL provider.

Starting with v0.7, schema evolution is managed through explicit EF Core migrations. The repository contains the migration history source files and model snapshot, and pins the `dotnet-ef` CLI through the local tool manifest so development and CI use a reproducible tool version.

In local development the API runs `MigrateAsync()` before development seeding. A new database therefore receives the complete schema through the normal migration pipeline rather than through `EnsureCreated`.

### Legacy v0.1–v0.6 databases

Earlier ReleaseGate versions used `EnsureCreated`, so existing local databases can contain the complete schema without an `__EFMigrationsHistory` table.

`DatabaseMigrationBootstrapper` handles this transition narrowly:

- if no ReleaseGate tables exist, normal migrations create the schema;
- if migration history already exists, normal migrations continue;
- if all expected legacy ReleaseGate tables exist and migration history does not, the initial migration is recorded as a baseline without recreating tables or deleting data;
- if only part of the expected legacy schema exists, startup fails instead of guessing which migration state the database represents.

This compatibility path is intentionally limited to the known pre-v0.7 schema. Once a database has been baselined, future changes use normal EF migration history.

The upgrade path was manually verified using a database created by the v0.6 `EnsureCreated` bootstrap. After switching to v0.7 while retaining the same PostgreSQL volume, the API started normally, existing projects and feature flags remained available, and `20260827090000_InitialSchema` was recorded in `__EFMigrationsHistory` with EF Core product version `10.0.4`.

CI also checks `dotnet ef migrations has-pending-model-changes` and applies the committed migrations against an empty PostgreSQL service. This catches both forgotten migrations and migrations that cannot build a clean database from scratch.

## Current hardening boundaries

Authentication, role-based authorization and versioned database migrations are now implemented for the control plane and persistence layer. The next hardening steps are expected to include:

- a production-grade application credential strategy for runtime snapshot access;
- integration with a real OIDC/OAuth identity provider while preserving the current subject/role model;
- SDK publishing/versioning;
- production deployment and operational documentation;
- push-based or streaming configuration delivery only if polling becomes an actual product constraint.

The goal remains the same: visible product behavior should be backed by a coherent end-to-end implementation rather than a broad set of stubs.
