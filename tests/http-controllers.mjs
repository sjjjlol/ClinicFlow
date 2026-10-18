import assert from 'node:assert/strict';
import { Client } from './auth.mjs';

const base = process.env.API_URL ?? 'http://127.0.0.1:5080';
const guest = new Client();
assert.equal((await guest.request('/api/openapi/v1.json')).status, 401);
assert.equal((await guest.request('/api/auth/me')).status, 401);
assert.equal((await guest.request('/api/auth/logout', {})).status, 401);
const client = await new Client().login('clinician');

async function raw(body, contentType = 'application/json') {
  return fetch(base + '/api/ois/commands/case', {
    method: 'POST',
    headers: {
      'Content-Type': contentType,
      Cookie: [...client.cookies].map(([k, v]) => `${k}=${v}`).join('; '),
      'X-CSRF-TOKEN': client.token,
      'Idempotency-Key': crypto.randomUUID(),
    },
    body,
  });
}
for (const body of ['{', 'null', '', '{"patientId":"bad"}', '{"startUtc":"not-a-date"}']) {
  const response = await raw(body);
  assert.equal(response.status, 400, body);
  const error = await response.json();
  assert.equal(error.code, 'invalid_request');
  assert.equal(error.correlationId, response.headers.get('X-Correlation-ID'));
  assert.equal(response.headers.get('X-Content-Type-Options'), 'nosniff');
}
assert.equal((await raw('{}', 'text/plain')).status, 415);

const nullText = await client.request('/api/ois/commands/case', { text: null }, crypto.randomUUID());
assert.equal(nullText.status, 400);
assert.equal(nullText.body.code, 'invalid_input', 'Explicit null still reaches service validation');
const missingKey = await client.request('/api/ois/commands/case', {});
assert.equal(missingKey.body.code, 'invalid_key');
const key = crypto.randomUUID();
const command = { PatientId: 1, Text: 'Controller compatibility' };
const created = await client.request('/api/ois/commands/case', command, key);
assert.equal(created.status, 200, JSON.stringify(created.body));
assert.equal((await client.request('/api/ois/commands/case', command, key)).body.targetId, created.body.targetId);
const snapshot = (await client.request('/api/ois/workspace')).body;
const audit = snapshot.audits.find(a => a.targetId === created.body.targetId);
assert.match(audit.atUtc, /Z$/, 'MVC must apply the UTC DateTime converter to database values');
assert.ok(snapshot.cases.some(c => c.id === created.body.targetId));
for (const path of ['/api/missing', '/legacy', '/legacy/path', '/fhir/r4/metadata']) {
  const response = await client.request(path);
  assert.equal(response.status, 404, path);
  assert.equal(response.body.code, 'not_found');
}
const schema = (await client.request('/api/openapi/v1.json')).body;
for (const path of ['/api/auth/csrf', '/api/auth/login', '/api/auth/logout', '/api/auth/me', '/api/health', '/api/ois/workspace', '/api/ois/commands/{operation}'])
  assert.ok(schema.paths[path], path);
assert.equal(Object.keys(schema.paths).length, 7, 'Fallback controllers must be hidden');
assert.ok(schema.paths['/api/ois/commands/{operation}'].post.parameters.some(p => p.name === 'Idempotency-Key'));
assert.ok(schema.paths['/api/auth/login'].post.parameters.some(p => p.name === 'X-CSRF-TOKEN'));

await client.request('/api/auth/logout', {});
assert.equal((await client.request('/api/auth/me')).status, 401);
await guest.csrf();
const emptyLogin = await guest.request('/api/auth/login', { username: null, password: null });
assert.equal(emptyLogin.status, 401);
assert.equal(emptyLogin.body.code, 'invalid_credentials');
// Optional because exhausting this IP's login quota also affects later browser tests.
if (process.env.VERIFY_LOGIN_RATE_LIMIT === '1') {
  let limited;
  for (let i = 0; i <= 30; i++) {
    const response = await guest.request('/api/auth/login', { username: 'missing', password: 'wrong' });
    if (response.status === 429) { limited = response; break; }
    assert.equal(response.status, 401);
  }
  assert.ok(limited, 'Controller login must retain its rate limit');
  assert.equal(limited.body.code, 'rate_limited');
  assert.ok(Number(limited.retryAfter) >= 1);
  assert.ok(limited.body.correlationId);
  console.log('PASS Controller login rate limit: 429, Retry-After and correlation ID');
}
console.log('PASS Controllers: binding/errors, null/defaults, UTC/camelCase, idempotency, 404, OpenAPI and logout');
