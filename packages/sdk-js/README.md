# @releasegate/sdk-js

JavaScript/TypeScript SDK for consuming evaluated ReleaseGate runtime snapshots.

The client fetches all evaluated flags for one project, environment and subject, then serves flag checks from memory. Repeated `isEnabled()` calls do not make additional network requests.

## Usage

```ts
import { ReleaseGateClient } from '@releasegate/sdk-js';

const client = new ReleaseGateClient({
  baseUrl: 'http://localhost:5080',
  projectKey: 'silva-commerce',
  environment: 'production',
});

await client.initialize('user-123');

if (client.isEnabled('new-checkout')) {
  // render the new checkout
}
```

Unknown flags return `false` by default. A different fallback can be supplied explicitly:

```ts
client.isEnabled('missing-flag', true);
```

## Refreshing configuration

`refresh()` downloads a new snapshot for the subject passed to `initialize()` and atomically replaces the in-memory flag map.

```ts
await client.refresh();
```

If a refresh request fails, the last valid snapshot remains available.

## Automatic refresh

Set `refreshInterval` in milliseconds to keep the cached snapshot up to date automatically. Automatic refresh starts after a successful `initialize()`.

```ts
const client = new ReleaseGateClient({
  baseUrl: 'http://localhost:5080',
  projectKey: 'silva-commerce',
  environment: 'production',
  refreshInterval: 30_000,
  onRefreshError: (error) => {
    console.error('ReleaseGate refresh failed', error);
  },
});

await client.initialize('user-123');

client.isEnabled('new-checkout');
```

The refresh loop waits for each request to finish before scheduling the next one, so slow requests do not create overlapping polls. Failed polls keep the last valid snapshot and are retried on the next interval.

Use `stop()` when the consumer no longer needs updates. `start()` can resume polling later when `refreshInterval` was configured.

```ts
client.stop();
client.start();
```

## Development

```bash
npm ci
npm run typecheck
npm test
```
