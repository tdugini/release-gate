import assert from 'node:assert/strict';
import test from 'node:test';
import { ReleaseGateClient } from '../dist/index.js';

function snapshot(flags, subjectKey = 'user-123') {
  return {
    projectKey: 'silva-commerce',
    environment: 'production',
    subjectKey,
    generatedAt: '2026-08-26T14:30:00Z',
    flags,
  };
}

function jsonResponse(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

test('initialize fetches one snapshot and serves repeated checks from memory', async () => {
  const requests = [];
  const client = new ReleaseGateClient({
    baseUrl: 'http://localhost:5080/',
    projectKey: 'silva-commerce',
    environment: 'production',
    fetch: async (input) => {
      requests.push(String(input));
      return jsonResponse(
        snapshot([
          { key: 'new-checkout', enabled: true },
          { key: 'new-header', enabled: false },
        ]),
      );
    },
  });

  assert.equal(client.initialized, false);

  await client.initialize('user-123');

  assert.equal(client.initialized, true);
  assert.equal(client.isEnabled('new-checkout'), true);
  assert.equal(client.isEnabled('new-header'), false);
  assert.equal(client.isEnabled('new-checkout'), true);
  assert.equal(requests.length, 1);
  assert.equal(
    requests[0],
    'http://localhost:5080/api/runtime/projects/silva-commerce/environments/production/snapshot?subjectKey=user-123',
  );
});

test('unknown flags use false by default or an explicit fallback', async () => {
  const client = new ReleaseGateClient({
    baseUrl: 'http://localhost:5080',
    projectKey: 'silva-commerce',
    environment: 'production',
    fetch: async () => jsonResponse(snapshot([])),
  });

  await client.initialize('user-123');

  assert.equal(client.isEnabled('missing-flag'), false);
  assert.equal(client.isEnabled('missing-flag', true), true);
});

test('refresh replaces the cached snapshot for the same subject', async () => {
  let requestCount = 0;
  const client = new ReleaseGateClient({
    baseUrl: 'http://localhost:5080',
    projectKey: 'silva-commerce',
    environment: 'production',
    fetch: async () => {
      requestCount += 1;
      return jsonResponse(
        snapshot([
          { key: 'new-checkout', enabled: requestCount > 1 },
        ]),
      );
    },
  });

  await client.initialize('user-123');
  assert.equal(client.isEnabled('new-checkout'), false);

  await client.refresh();

  assert.equal(client.isEnabled('new-checkout'), true);
  assert.equal(requestCount, 2);
});

test('a failed refresh preserves the last valid snapshot', async () => {
  let shouldFail = false;
  const client = new ReleaseGateClient({
    baseUrl: 'http://localhost:5080',
    projectKey: 'silva-commerce',
    environment: 'production',
    fetch: async () =>
      shouldFail
        ? jsonResponse({ message: 'unavailable' }, 503)
        : jsonResponse(snapshot([{ key: 'new-checkout', enabled: true }])),
  });

  await client.initialize('user-123');
  shouldFail = true;

  await assert.rejects(() => client.refresh(), /status 503/);
  assert.equal(client.isEnabled('new-checkout'), true);
});

test('refresh before initialize fails without making a request', async () => {
  let requestCount = 0;
  const client = new ReleaseGateClient({
    baseUrl: 'http://localhost:5080',
    projectKey: 'silva-commerce',
    environment: 'production',
    fetch: async () => {
      requestCount += 1;
      return jsonResponse(snapshot([]));
    },
  });

  await assert.rejects(() => client.refresh(), /initialized/);
  assert.equal(requestCount, 0);
});
