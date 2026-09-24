import assert from 'node:assert/strict';
import { Client } from './auth.mjs';
const guest = new Client();
assert.equal((await guest.request('/api/agent/config')).status, 401);
for (const role of ['admin', 'taskoperator']) {
  const c = await new Client().login(role);
  for (const [path, body] of [
    ['/api/agent/config', undefined],
    ['/api/agent/messages', {message: '直接创建', selectedPatientId: 1}],
    ['/api/agent/forged/confirm', {candidateId: 'forged'}],
  ]) assert.equal((await c.request(path, body)).status, 403);
}
const c = await new Client().login();
assert.equal((await c.request('/api/agent/config')).status, 200);
assert.equal((await c.request('/api/agent/forged/confirm', {candidateId: 'forged'})).status, 404);
assert.equal((await c.request('/api/agent/messages', {message: ''})).status, 400);
c.token = '';
assert.equal((await c.request('/api/agent/messages', {message: '预约'})).status, 400);
console.log('PASS agent HTTP: authentication, Scheduler-only authorization, CSRF, forged session, input validation');
