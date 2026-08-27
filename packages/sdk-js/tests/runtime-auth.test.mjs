import assert from 'node:assert/strict';
import test from 'node:test';
import { ReleaseGateClient } from '../dist/index.js';

test('runtime API key is sent with snapshot requests', async () => {
  let receivedApiKey = null;

  const client = new ReleaseGateClient({
    baseUrl: 'http://localhost:5080',
    projectKey: 'silva-commerce',
    environment: 'production',
    apiKey: 'runtime-secret',
    fetch: async (_input, init) => {
      receivedApiKey = new Headers(init?.headers).get('X-ReleaseGate-Key');

      return new Response(
        JSON.stringify({
          projectKey: 'silva-commerce',
          environment: 'production',
          subjectKey: 'user-123',
          generatedAt: '2026-08-27T09:00:00Z',
          flags: [],
        }),
        {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        },
      );
    },
  });

  await client.initialize('user-123');

  assert.equal(receivedApiKey, 'runtime-secret');
});
