import assert from 'node:assert/strict';
import { Client } from './auth.mjs';
import { routes } from './ois-routes.mjs';

const base = process.env.API_URL ?? 'http://127.0.0.1:5080';
const guest = new Client();
assert.equal((await guest.request('/api/openapi/v1.json')).status, 401);
assert.equal((await guest.request('/api/auth/me')).status, 401);
assert.equal((await guest.request('/api/auth/logout', {})).status, 401);
const client = await new Client().login('clinician');

async function raw(body, contentType = 'application/json') {
  return fetch(base + '/api/ois/cases', {
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
for (const body of ['{', 'null', '', '{"patientId":"bad"}', '{"patientId":{}}']) {
  const response = await raw(body);
  assert.equal(response.status, 400, body);
  const error = await response.json();
  assert.equal(error.code, 'invalid_request');
  assert.equal(error.correlationId, response.headers.get('X-Correlation-ID'));
  assert.equal(response.headers.get('X-Content-Type-Options'), 'nosniff');
}
assert.equal((await raw('{}', 'text/plain')).status, 415);

const nullText = await client.request('/api/ois/cases', { text: null }, crypto.randomUUID());
assert.equal(nullText.status, 400);
assert.equal(nullText.body.code, 'invalid_input', 'Explicit null still reaches service validation');
const missingKey = await client.request('/api/ois/cases', {});
assert.equal(missingKey.body.code, 'invalid_key');
const key = crypto.randomUUID();
const command = { PatientId: 1, Text: 'Controller compatibility' };
const created = await client.request('/api/ois/cases', command, key);
assert.equal(created.status, 200, JSON.stringify(created.body));
assert.equal((await client.request('/api/ois/cases', command, key)).body.targetId, created.body.targetId);
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
for (const path of ['/api/auth/csrf', '/api/auth/login', '/api/auth/logout', '/api/auth/me', '/api/health', '/api/ois/workspace'])
  assert.ok(schema.paths[path], path);
for (const { path, method, target } of Object.values(routes)) {
  const action = schema.paths[path]?.[method.toLowerCase()];
  assert.ok(action, `Resource action documented: ${method} ${path}`);
  assert.ok(action.requestBody, path);
  assert.ok(action.parameters.some(p => p.name === 'Idempotency-Key'), path);
  assert.ok(action.parameters.some(p => p.name === 'X-CSRF-TOKEN'), path);
  if (target) assert.ok(action.parameters.some(p => p.in === 'path' && p.name === 'id'));
}
assert.ok(!Object.keys(schema.paths).some(path => path.includes('/commands/')));
assert.equal(Object.keys(schema.paths).length, Object.keys(routes).length + 6);
assert.ok(schema.paths['/api/auth/login'].post.parameters.some(p => p.name === 'X-CSRF-TOKEN'));
const unknown = await client.request('/api/ois/commands/unknown-operation', {}, crypto.randomUUID());
assert.equal(unknown.status, 404);
assert.equal(unknown.body.code, 'not_found');

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

// New resource contracts: path IDs are authoritative, and PUT carries the same protections.
await client.login('clinician');
const noteKey = crypto.randomUUID();
const notePath = `/api/ois/cases/${created.body.targetId}/notes`;
const note = await client.request(notePath, { id: 'body-must-not-retarget', version: 1, text: 'Resource note' }, noteKey);
assert.equal(note.status, 200, JSON.stringify(note.body));
assert.equal(note.body.targetId, created.body.targetId);
const draftPath = `/api/ois/cases/${created.body.targetId}/prescription-draft`;
const draftInput = { version: 1, draftVersion: 0, draftJson: '{"text":"existing form fields"}' };
const draftKey = crypto.randomUUID();
const draft = await client.request(draftPath, draftInput, draftKey, 'PUT');
assert.equal(draft.status, 200, JSON.stringify(draft.body));
assert.equal((await client.request(draftPath, draftInput, draftKey, 'PUT')).body.targetId, draft.body.targetId);
assert.equal((await client.request(draftPath, { ...draftInput, draftJson: '{}' }, draftKey, 'PUT')).body.code, 'idempotency_conflict');
const saved = (await client.request('/api/ois/workspace')).body;
assert.equal(saved.drafts.find(d => d.id === draft.body.targetId).payload, draftInput.draftJson);
assert.equal(JSON.parse(saved.audits.find(a => a.action === 'note' && a.targetId === created.body.targetId).detail).command.text, 'Resource note');
client.token = '';
assert.equal((await client.request(draftPath, draftInput, crypto.randomUUID(), 'PUT')).body.code, 'csrf');
await client.csrf();
assert.equal((await client.request(`/api/ois/cases/${created.body.targetId}/plan-draft`, draftInput, crypto.randomUUID(), 'PUT')).status, 403);
for (const old of ['case', 'prescribe', 'plan', 'review', 'book', 'reschedule', 'restore', 'cancel', 'no-show', 'validate', 'machine', 'consult', 'start', 'finish', 'not-executed', 'read-task', 'resolve-task', 'correction', 'save-draft', 'terminate', 'note'])
  assert.equal((await client.request(`/api/ois/commands/${old}`, {}, crypto.randomUUID())).status, 404);
const createSchema = schema.components.schemas.CreateCaseRequest;
assert.ok(createSchema.properties.patientId);
assert.equal(createSchema.properties.resourceId, undefined);
assert.equal(schema.components.schemas.ReviewPlanRequest.properties.id, undefined);
assert.equal(schema.components.schemas.BookExperimentRequest.properties.patientId, undefined);
console.log('PASS resource contracts: authoritative path IDs, typed schemas, PUT CSRF/roles/replay/conflict, drafts/history and removed legacy routes');
