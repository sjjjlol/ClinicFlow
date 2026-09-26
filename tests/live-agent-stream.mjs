import assert from 'node:assert/strict';
import { Client } from './auth.mjs';

if (process.env.RUN_LIVE_AGENT !== '1') throw new Error('Set RUN_LIVE_AGENT=1 to use the paid model API');
const base = process.env.API_URL ?? 'http://127.0.0.1:5080';
const client = new Client();
await client.csrf();
const registered = await client.request('/api/auth/register', {
  username: 'live_stream_' + crypto.randomUUID().slice(0, 8), password: '12345678', displayName: '流式联调虚构用户',
});
assert.equal(registered.status, 201);
await client.csrf();
assert.equal((await client.request('/api/agent/config')).body.engine, 'pi-agent-core');
const started = Date.now();
const events = [];
async function stream(path, body) {
  const response = await fetch(base + path, { method: 'POST', headers: {
    'Content-Type': 'application/json', 'X-CSRF-TOKEN': client.token,
    Cookie: [...client.cookies].map(([k, v]) => `${k}=${v}`).join('; '),
  }, body: JSON.stringify(body) });
  assert.equal(response.status, 200);
  const reader = response.body.getReader(), decoder = new TextDecoder();
  let buffer = '', reply;
  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    let index;
    while ((index = buffer.indexOf('\n\n')) >= 0) {
      const event = JSON.parse(buffer.slice(0, index).slice(6));
      buffer = buffer.slice(index + 2);
      assert.notEqual(event.type, 'error', event.message);
      events.push({ type: event.type, atMs: Date.now() - started });
      if (event.type === 'result') reply = event.reply;
    }
  }
  assert.ok(reply, 'A stream must end with a complete result');
  return reply;
}
const proposal = await stream('/api/agent/messages/stream', {
  message: '帮我本人查询2031年1月6日下午13:00到16:00之间连续45分钟的预约，只用预约室A。请给出候选，不要直接创建。',
});
assert.equal(proposal.status, 'proposed', proposal.message);
assert.ok(proposal.candidates.length > 0);
assert.ok(proposal.candidates.every(c => c.booking.patientId === registered.body.patientId && c.booking.resourceId === 1));
assert.equal((await client.request('/api/appointments')).body.total, 0);
const deltas = events.filter(e => e.type === 'delta');
assert.ok(deltas.length > 1, 'Expected real incremental model output');
assert.ok(deltas[0].atMs < events.at(-1).atMs);
const candidate = proposal.candidates[0];
const created = await stream(`/api/agent/${proposal.sessionId}/confirm/stream`, { candidateId: candidate.id });
assert.equal(created.status, 'created');
const replay = await stream(`/api/agent/${proposal.sessionId}/confirm/stream`, { candidateId: candidate.id });
assert.equal(created.appointment.id, replay.appointment.id);
assert.equal((await client.request('/api/appointments')).body.total, 1);
const cancelled = await client.request(`/api/appointments/${created.appointment.id}/cancel`, { version: created.appointment.version }, crypto.randomUUID());
assert.equal(cancelled.body.status, 'Cancelled');
console.log(JSON.stringify({ result: 'PASS', engine: 'pi-agent-core', textChunks: deltas.length,
  firstTextMs: deltas[0].atMs, proposalMs: events.find(e => e.type === 'result').atMs,
  profileBound: true, explicitConfirmation: true, replayedOnce: true, testBookingCancelled: true }));
