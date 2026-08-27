# @releasegate/sdk-js

JavaScript/TypeScript SDK for consuming evaluated ReleaseGate feature flags from the application-facing runtime API.

The client fetches all evaluated flags for one project, environment and subject, then serves flag checks from memory. Repeated `isEnabled()` calls do not make additional network requests.

## Install

```bash
npm install @releasegate/sdk-js
```

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

## Runtime credentials

`apiKey` is a machine-to-machine credential for the ReleaseGate runtime API. It is separate from the human control-plane bearer tokens used by operators and reviewers.

Do not embed a privileged runtime key in a public browser bundle. Use the SDK from a trusted application/server boundary or issue a credential with an intentionally limited project scope.

## Refreshing configuration

`refresh()` revalidates the current runtime snapshot for the subject passed to `initialize()`.

```ts
await client.refresh();
```

ReleaseGate runtime responses include an ETag. After the first successful request, the SDK sends that value back through `If-None-Match` on subsequent refreshes. If the evaluated flag set has not changed, the API responds with `304 Not Modified` and the SDK keeps the existing in-memory snapshot without downloading or parsing another payload.

If the configuration has changed, the API returns a fresh snapshot and ETag, and the SDK atomically replaces its in-memory flag map. If a refresh request fails, the last valid snapshot remains available.

## Automatic refresh

Set `refreshInterval` in milliseconds to keep the cached snapshot up to date automatically. Automatic refresh starts after a successful `initialize()`.

The refresh loop waits for each request to finish before scheduling the next one, so slow requests do not create overlapping polls. Failed polls keep the last valid snapshot and are retried on the next interval.

Use `stop()` when the consumer no longer needs updates. `start()` can resume polling later when `refreshInterval` was configured.

## Releases

The SDK version is independent from the ReleaseGate application milestone version. SDK releases use tags in the form:

```text
sdk-js-v0.1.0
```

The release workflow verifies that the tag version exactly matches `package.json`, runs type checking and tests, validates the npm package contents, and publishes to npm when the repository has an `NPM_TOKEN` secret with permission to publish `@releasegate/sdk-js`.

Before creating a new SDK tag, update the `version` field in `package.json` and its lockfile entry in the same commit.

## Development

```bash
npm ci
npm run typecheck
npm test
npm run pack:check
```
