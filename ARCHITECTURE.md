# ReleaseGate architecture

## Why this project exists

ReleaseGate is intentionally modeled as an internal developer platform rather than a generic CRUD application. The domain is small enough to understand quickly, while still creating room for real engineering concerns: environment isolation, rollout evaluation, auditability, approvals, runtime delivery, SDK design, authentication, authorization, schema evolution and deployment.

## System boundaries

ReleaseGate contains three main application boundaries:

- `ReleaseGate.Api` owns the domain model, persistence, authenticated control-plane HTTP API and runtime delivery API;
- `apps/web` is the authenticated operator control plane used to manage projects, flags, environment configuration and production reviews;
- `packages/sdk-js` is the application-facing JavaScript/TypeScript SDK used by consumers to read evaluated feature flags.

PostgreSQL is the system of record.

The control plane and runtime API deliberately expose different representations and security concerns. Operators need configuration, rollout, audit and review detail. Applications only need the final evaluated value of each flag for their current subject.

## Authentication and authorization boundaries

ReleaseGate treats human control-plane access and application runtime access as separate trust boundaries.

### Control plane

Control-plane routes require bearer authentication. The current implementation uses server-configured static tokens because the authorization model and control-plane boundary are implemented independently from any external identity provider.

An authenticated control-plane identity contains:

- a stable subject used for authorization and audit attribution;
- a display name used by the React control plane;
- one or more roles.

The current roles are:

- `operator` — may create projects and flags and mutate environment configuration;
- `reviewer` — may approve or reject pending production changes created by another identity.

Read-only control-plane access requires authentication, while mutation endpoints enforce the appropriate role. Audit actors are derived from the authenticated subject instead of caller-provided headers.

Production review also enforces separation of duties: the identity that requested a pending production change cannot approve or reject that same change.

The React control plane resolves the current identity through `/api/auth/me`, persists the development access token locally, exposes assigned roles in the shell, and removes or disables actions the current role cannot perform. The API remains the source of truth for authorization; UI gating is only a usability layer.

### Runtime access

Runtime snapshot requests use a dedicated machine-to-machine API key supplied through `X-ReleaseGate-Key`.

Runtime credentials are configured separately from control-plane bearer tokens and contain:

- the secret API key;
- a client identifier;
- one or more allowed project keys, with `*` supported for wildcard project access.

The runtime validator compares API keys using a constant-time byte comparison. Missing or unknown credentials produce `401 Unauthorized`; a valid credential that is not allowed to access the requested project produces `403 Forbidden`.

A control-plane token does not grant runtime access, and a runtime key does not grant control-plane access. Hosted deployments inject secret material through environment variables so the runtime contract does not depend on committed production credentials.

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

Application consumers use the runtime snapshot endpoint:

```text
GET /api/runtime/projects/{projectKey}/environments/{environmentKey}/snapshot?subjectKey={subjectKey}
X-ReleaseGate-Key: <runtime-api-key>
```

The runtime snapshot contains only evaluated flag keys and boolean values. Rollout percentages, buckets, audit metadata and pending production changes stay behind the control-plane boundary.

## Runtime configuration delivery

The runtime snapshot is designed to support many local flag checks from one network request.

Responses include a weak HTTP ETag derived from the evaluated flag set for the project, environment and subject. Consumers can revalidate with `If-None-Match`. When the evaluated configuration is unchanged, the API returns `304 Not Modified` without another JSON payload.

Runtime responses are marked `private, no-cache` so clients may retain the representation but must revalidate it rather than allowing shared caches to serve subject-specific configuration.

## JavaScript SDK

`@releasegate/sdk-js` initializes by downloading one runtime snapshot for a subject and stores the evaluated flags in memory.

The client requires a runtime `apiKey` and sends it as `X-ReleaseGate-Key` on snapshot requests. Human control-plane tokens are not part of the SDK configuration.

`isEnabled(flagKey)` is a local lookup and does not make a network request. Manual `refresh()` and optional automatic polling revalidate the snapshot using its ETag. A `304` keeps the current in-memory snapshot, while a changed response atomically replaces the flag map.

Automatic polling schedules the next refresh only after the previous request finishes, preventing overlapping requests. Failed refreshes preserve the last valid snapshot and can be surfaced through the consumer error callback without stopping the refresh loop.

### SDK release boundary

The SDK package version is intentionally independent from the ReleaseGate application milestone version.

Tags use the form `sdk-js-v<package-version>`. The release workflow rejects a tag that does not exactly match `packages/sdk-js/package.json`, then runs type checking, tests and `npm pack --dry-run` before publishing with npm provenance.

Registry publication is an explicit release action and requires an `NPM_TOKEN` plus permission for the `@releasegate/sdk-js` package scope. Pull-request CI validates that the package remains packable without publishing anything.

## Persistence

ReleaseGate uses Entity Framework Core with the Npgsql PostgreSQL provider.

Starting with v0.7, schema evolution is managed through explicit EF Core migrations. The repository contains the migration source files and model snapshot and pins `dotnet-ef` through the local tool manifest.

For relational databases, the API applies committed migrations during startup. Development additionally runs the narrow legacy pre-v0.7 baseline compatibility path before migration and seeds local demo data afterward.

The legacy bootstrap is intentionally not used in deployed environments. A deployed database is expected to be created and evolved through EF migration history.

For the current single-instance self-hosted deployment, startup migration keeps installation simple. If ReleaseGate later runs multiple API replicas, migration execution should move into a dedicated release job so replicas do not compete to change schema.

CI checks for pending model changes and independently applies committed migrations to an empty PostgreSQL service.

## Deployment boundary

v0.9 packages the API and web control plane as separate production container images.

The production-like Compose topology is:

```text
client
  |
  v
nginx/web
  |
  v
ReleaseGate.Api
  |
  v
PostgreSQL
```

Only nginx is published to the host by default. PostgreSQL and the API remain on the internal Compose network.

The production React build uses same-origin API requests. nginx serves the SPA, routes client-side navigation back to `index.html`, and proxies `/api/*` plus `/health` to the API service. This avoids coupling the browser bundle to a deployment-specific API hostname and removes the need for production browser CORS configuration in the single-host topology.

Deployment configuration is injected through environment variables. The committed `.env.production.example` documents required values but does not contain usable production secrets.

## Deployment verification

CI treats the production-like stack as an integration boundary rather than validating Dockerfiles independently.

After API, web and SDK jobs pass, CI builds and starts the Compose stack, waits for `/health` through nginx, creates a project and feature flag through authenticated control-plane requests, changes development configuration, reads the evaluated flag through the runtime API key, and verifies that the runtime endpoint rejects a request without a key.

That smoke test exercises container builds, networking, reverse proxying, production migrations, PostgreSQL persistence, control-plane authentication and runtime authentication together.

## Current hardening boundaries

Authentication, RBAC, runtime credentials, versioned migrations, SDK packaging and a production-like single-host deployment are implemented.

The remaining v1.0 work is intentionally product-facing and operational rather than another large architecture expansion. Potential post-v1.0 hardening includes:

- integration with a real OIDC/OAuth identity provider while preserving the current subject/role model;
- external secret-manager integration and credential rotation;
- migration orchestration for horizontally scaled API deployments;
- push-based or streaming configuration delivery only if polling becomes an actual constraint.

The goal remains the same: visible product behavior should be backed by a coherent end-to-end implementation rather than a broad set of stubs.
