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
console.log('PASS agent HTTP: authentication, staff role authorization, CSRF, forged session, input validation');

// Invalid inputs exercise the real SSE endpoint without spending model tokens.
const base = process.env.API_URL ?? 'http://127.0.0.1:5080';
const streamClient = await new Client().login();
const headers = {'Content-Type':'application/json','Cookie':[...streamClient.cookies].map(([k,v])=>`${k}=${v}`).join('; '),'X-CSRF-TOKEN':streamClient.token};
const stream = await fetch(base+'/api/agent/messages/stream', {method:'POST',headers,body:JSON.stringify({message:''})});
assert.equal(stream.status,200);
assert.ok(stream.headers.get('content-type').includes('text/event-stream'));
const frames = (await stream.text()).split('\n\n').filter(Boolean).map(x=>JSON.parse(x.slice(6)));
assert.equal(frames[0].type,'progress');
assert.equal(frames.at(-1).type,'error');
assert.equal(frames.at(-1).code,'invalid_message');
const noCsrf = await fetch(base+'/api/agent/messages/stream', {method:'POST',headers:{...headers,'X-CSRF-TOKEN':''},body:'{}'});
assert.equal(noCsrf.status,400);
console.log('PASS streaming HTTP: SSE framing, explicit terminal error and CSRF');
