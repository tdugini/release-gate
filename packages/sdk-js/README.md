# @releasegate/sdk-js

JavaScript/TypeScript SDK for consuming evaluated ReleaseGate feature flags from the application-facing runtime API.

The client downloads one evaluated snapshot for a project, environment and subject, stores it in memory and serves repeated flag checks locally. Calls to `isEnabled()` do not perform additional network requests.

Current package version in this repository: **0.1.0**.

## Install

When the package is available from the configured npm registry:

```bash
npm install @releasegate/sdk-js
```

For repository development, use the package directly from `packages/sdk-js` and run the validation commands shown below.

## Usage

```ts
import { ReleaseGateClient } from '@releasegate/sdk-js';

const client = new ReleaseGateClient({
  baseUrl: 'https://releasegate.example.com',
  projectKey: 'silva-commerce',
  environment: 'production',
  apiKey: process.env.RELEASEGATE_RUNTIME_KEY!,
  refreshInterval: 30_000,
});

await client.initialize('customer-1042');

if (client.isEnabled('new-checkout')) {
  // expose the new release path
}
```

Unknown flags return `false` by default. A different fallback can be supplied explicitly:

```ts
client.isEnabled('missing-flag', true);
```

## Runtime contract

The SDK reads the evaluated snapshot endpoint:

```text
GET /api/runtime/projects/{projectKey}/environments/{environmentKey}/snapshot?subjectKey={subjectKey}
```

and authenticates with:

```http
X-ReleaseGate-Key: <runtime-api-key>
```

The snapshot contains evaluated boolean values rather than control-plane configuration, rollout percentages, pending changes or audit metadata.

## Runtime credentials

`apiKey` is a machine-to-machine credential for the ReleaseGate runtime API. It is separate from the bearer tokens used by human control-plane identities.

Runtime credentials may be scoped to specific project keys. A valid key outside its allowed project scope receives `403 Forbidden`; a missing or unknown key receives `401 Unauthorized`.

Do not embed a privileged runtime key in a public browser bundle. Use the SDK from a trusted application/server boundary or issue credentials whose scope is intentionally appropriate for the consumer.

The read-only token embedded in the ReleaseGate portfolio demo is a **control-plane demo credential**, not a runtime API key, and cannot be used by this SDK.

## Initialization

`initialize(subjectKey)` performs the first snapshot request and stores the evaluated flags in memory.

```ts
await client.initialize('customer-1042');
```

After successful initialization, normal flag checks are local:

```ts
const enabled = client.isEnabled('new-checkout');
```

This keeps application request paths independent from a network call per flag check.

## ETag revalidation

ReleaseGate snapshot responses include an ETag.

After the first successful request, the SDK sends the current value through `If-None-Match` during refreshes.

If the evaluated configuration has not changed, ReleaseGate returns:

```text
304 Not Modified
```

The SDK then keeps the existing in-memory snapshot without downloading or parsing another JSON payload.

If the configuration changed, the API returns a fresh snapshot and ETag and the client atomically replaces its in-memory flag map.

## Manual refresh

Revalidate the current subject explicitly with:

```ts
await client.refresh();
```

If the refresh fails, the last valid snapshot remains available instead of being cleared.

## Automatic refresh

Configure `refreshInterval` in milliseconds:

```ts
const client = new ReleaseGateClient({
  baseUrl: 'https://releasegate.example.com',
  projectKey: 'silva-commerce',
  environment: 'production',
  apiKey: process.env.RELEASEGATE_RUNTIME_KEY!,
  refreshInterval: 30_000,
});
```

Automatic polling starts after a successful `initialize()`.

The refresh loop waits until the current request finishes before scheduling the next one, so slow requests cannot create overlapping polls.

Failed polls preserve the last valid snapshot and can be surfaced through the configured error callback. The loop continues on the next interval.

Use:

```ts
client.stop();
```

to stop automatic refresh. Call `start()` to resume it when a refresh interval is configured.

## Failure behavior

The client is designed around a last-known-good snapshot:

- initialization must succeed before the client has runtime state;
- failed refreshes do not erase previously evaluated flags;
- `isEnabled()` remains a synchronous local lookup;
- unknown flags use the configured fallback behavior.

Application-specific decisions about logging, alerting or fallback policy remain the consumer's responsibility.

## Package lifecycle

The SDK version is independent from the ReleaseGate application version.

Release tags use:

```text
sdk-js-v<package-version>
```

For example:

```text
sdk-js-v0.1.0
```

Before creating a tag, update the `version` field in `packages/sdk-js/package.json` and the corresponding lockfile entry in the same commit.

`.github/workflows/sdk-release.yml` requires the tag version to match `package.json`, then runs type checking, tests and package-content validation before npm publication.

Publishing requires an `NPM_TOKEN` repository secret with permission to publish the `@releasegate/sdk-js` scope. The ReleaseGate application itself does not depend on the SDK being published to npm.

## Development

From `packages/sdk-js`:

```bash
npm ci
npm run typecheck
npm test
npm run pack:check
```

`npm test` builds the package and runs the Node test suite. `npm run pack:check` verifies the files that would be included in the npm package without publishing anything.

## Related documentation

See the repository-level documentation for the surrounding runtime model:

- `README.md` — product overview and local development;
- `ARCHITECTURE.md` — runtime/control-plane boundaries and delivery design;
- `DEPLOYMENT.md` — self-hosted deployment, credentials and CI/CD operations.
