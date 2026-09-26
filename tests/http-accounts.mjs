import assert from 'node:assert/strict';
import {Client} from './auth.mjs';

const password = 'account-test-only-password';
const suffix = crypto.randomUUID().slice(0, 8);
const alice = new Client(), bob = new Client();
const username = 'alice_' + suffix;
assert.equal((await alice.request('/api/auth/register', {username, password, displayName: '测试甲'})).status, 400);
await alice.csrf();
const registered = await alice.request('/api/auth/register', {
  username: '  ' + username.toUpperCase() + '  ', password, displayName: '测试甲', role: 'Admin', patientId: 1,
});
assert.equal(registered.status, 201, JSON.stringify(registered));
assert.equal(registered.body.name, username);
assert.equal(registered.body.role, 'Booker');
assert.ok(registered.body.patientId > 2);
await alice.csrf();
await bob.csrf();
const other = await bob.request('/api/auth/register', {username: 'bob_' + suffix, password, displayName: '测试乙'});
assert.equal(other.status, 201);
await bob.csrf();
const duplicate = new Client();
await duplicate.csrf();
assert.equal((await duplicate.request('/api/auth/register', {username, password, displayName: '重复'})).body.code, 'username_taken');
assert.equal((await alice.request('/api/auth/me')).body.patientId, registered.body.patientId);
assert.deepEqual((await alice.request('/api/patients')).body.map(x => x.id), [registered.body.patientId]);
assert.equal((await alice.request('/fhir/r4/Patient/' + other.body.patientId)).status, 404);
assert.equal((await alice.request('/fhir/r4/Patient/1')).status, 404);
assert.equal((await alice.request('/fhir/r4/Patient/' + registered.body.patientId)).status, 200);
for (const path of ['/api/sync', '/api/sync/forged/attempts'])
  assert.equal((await alice.request(path)).status, 403);

assert.equal((await alice.request('/api/agent/config')).status, 200);
assert.equal((await alice.request('/api/agent/config')).body.demoEnabled, false);
assert.equal((await alice.request('/api/agent/messages', {message:'预约', selectedPatientId: other.body.patientId})).status, 403);
assert.equal((await alice.request('/api/agent/forged/confirm', {candidateId:'forged'})).status, 404);

const start = new Date(Date.UTC(2037, 0, 1) + Math.floor(Math.random() * 100000) * 900000);
const booking = {patientId: registered.body.patientId, resourceId: 1, startUtc: start.toISOString(), endUtc: new Date(+start + 3600000).toISOString()};
assert.equal((await alice.request('/api/appointments', {...booking, patientId: other.body.patientId}, crypto.randomUUID())).status, 403);
assert.equal((await alice.request('/api/appointments', {...booking, startUtc: '2000-01-01T00:00:00Z', endUtc: '2000-01-01T01:00:00Z'}, crypto.randomUUID())).body.code, 'appointment_started');
const createKey = crypto.randomUUID();
let a = (await alice.request('/api/appointments', booking, createKey)).body;
assert.equal(a.status, 'Pending');
assert.deepEqual((await alice.request('/api/appointments', booking, createKey)).body, a);
assert.equal((await bob.request('/api/appointments')).body.total, 0);
assert.equal((await bob.request('/api/appointments/' + a.id)).status, 404);
assert.equal((await bob.request('/fhir/r4/Appointment/' + a.id)).status, 404);
for (const action of ['reschedule', 'cancel'])
  assert.equal((await bob.request(`/api/appointments/${a.id}/${action}`, {version: a.version, resourceId: 2, startUtc: booking.startUtc, endUtc: booking.endUtc}, crypto.randomUUID())).status, 404);
for (const action of ['confirm', 'complete', 'complete-task'])
  assert.equal((await alice.request(`/api/appointments/${a.id}/${action}`, {version: a.version}, crypto.randomUUID())).status, 403);
const slots = (await bob.request(`/api/resources/1/slots?start=${booking.startUtc}&end=${booking.endUtc}`)).body;
assert.ok(slots.length > 0);
assert.ok(slots.every(x => !('appointmentId' in x) && !('patientId' in x)));
a = (await alice.request(`/api/appointments/${a.id}/reschedule`, {version: a.version, resourceId: 2, startUtc: booking.startUtc, endUtc: booking.endUtc}, crypto.randomUUID())).body;
assert.equal(a.version, 2);
a = (await alice.request(`/api/appointments/${a.id}/cancel`, {version: a.version}, crypto.randomUUID())).body;
assert.equal(a.status, 'Cancelled');

const scheduler = await new Client().login(), operator = await new Client().login('taskoperator'), admin = await new Client().login('admin');
// Staff may enter historical appointments; self-service users cannot backdate.
const oldStart = new Date(Date.UTC(2015, 0, 1) + Math.floor(Math.random() * 100000) * 900000);
a = (await scheduler.request('/api/appointments', {...booking, startUtc: oldStart.toISOString(), endUtc: new Date(+oldStart + 3600000).toISOString()}, crypto.randomUUID())).body;
assert.equal(a.status, 'Pending');
assert.equal((await scheduler.request(`/api/appointments/${a.id}/complete`, {version: a.version}, crypto.randomUUID())).body.code, 'invalid_state');
let detail = (await scheduler.request('/api/appointments/' + a.id)).body;
for (const task of detail.tasks)
  a = (await operator.request(`/api/appointments/${a.id}/complete-task`, {version: a.version, taskId: task.id}, crypto.randomUUID())).body;
a = (await scheduler.request(`/api/appointments/${a.id}/confirm`, {version: a.version}, crypto.randomUUID())).body;
assert.equal(a.status, 'Confirmed');
for (const client of [alice, bob, operator, admin])
  assert.equal((await client.request(`/api/appointments/${a.id}/complete`, {version: a.version}, crypto.randomUUID())).status, 403);
const key = crypto.randomUUID(), previous = a;
a = (await scheduler.request(`/api/appointments/${a.id}/complete`, {version: a.version}, key)).body;
assert.equal(a.status, 'Completed');
assert.deepEqual((await scheduler.request(`/api/appointments/${a.id}/complete`, {version: previous.version}, key)).body, a);
assert.equal((await scheduler.request(`/api/appointments/${a.id}/cancel`, {version: a.version}, crypto.randomUUID())).body.code, 'invalid_state');
assert.equal((await alice.request('/fhir/r4/Appointment/' + a.id)).body.status, 'fulfilled');
detail = (await alice.request('/api/appointments/' + a.id)).body;
assert.equal(detail.audit.filter(x => x.action === 'Completed').length, 1);
assert.equal(detail.sync.filter(x => x.version === a.version).length, 1);
assert.equal((await alice.request('/api/appointments?status=Completed')).body.items[0].id, a.id);
assert.equal((await bob.request('/api/appointments/' + a.id)).status, 404);
await alice.request('/api/auth/logout', {});
assert.equal((await alice.request('/api/appointments')).status, 401);
await alice.csrf();
assert.equal((await alice.request('/api/auth/login', {username, password})).status, 200);
assert.equal((await alice.request('/api/auth/me')).body.patientId, registered.body.patientId);
console.log('PASS accounts: registration, normalization, CSRF, role injection, ownership, FHIR isolation, self-service, login and complete lifecycle');
