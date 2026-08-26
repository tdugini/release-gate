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

If a refresh request fails, the last valid snapshot remains available. Automatic refresh/polling is intentionally kept separate from this first SDK slice.

## Development

```bash
npm install
npm run typecheck
npm test
```
