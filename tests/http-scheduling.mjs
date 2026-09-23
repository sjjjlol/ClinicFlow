import assert from 'node:assert/strict';
import {Client} from './auth.mjs';
const c=await new Client().login();
const start=new Date(Date.UTC(2035,0,1)+Math.floor(Math.random()*300000)*900000).toISOString();
const body={patientId:1,resourceId:1,startUtc:start,endUtc:new Date(Date.parse(start)+3600000).toISOString()};
for(const role of ['admin','taskoperator']){const x=await new Client().login(role);assert.equal((await x.request('/api/appointments',body,crypto.randomUUID())).status,403);}
const key=crypto.randomUUID();const a=await c.request('/api/appointments',body,key);assert.equal(a.status,200,JSON.stringify(a));assert.ok(a.body.startUtc.endsWith('Z'));
assert.deepEqual((await c.request('/api/appointments',body,key)).body,a.body);
assert.equal((await c.request('/api/appointments',body,crypto.randomUUID())).body.code,'slot_conflict');
const d=await c.request('/api/appointments/'+a.body.id);assert.equal(d.body.tasks.length,2);assert.equal(d.body.audit.length,1);assert.equal(d.body.sync.length,1);
const list=await c.request('/api/appointments?size=1');assert.equal(list.body.items.length,1);assert.ok(list.body.total>=1);
const slots=await c.request(`/api/resources/1/slots?start=${body.startUtc}&end=${body.endUtc}`);assert.equal(slots.body.length,4);
console.log('PASS A02/A10/A11/A18/A19 HTTP booking, replay, conflict, permission, pagination, UTC');
for(const role of ['admin','taskoperator']){const x=await new Client().login(role);for(const action of ['cancel','reschedule'])assert.equal((await x.request(`/api/appointments/${a.body.id}/${action}`,{version:1},crypto.randomUUID())).status,403);}
const move={version:1,resourceId:2,startUtc:body.startUtc,endUtc:body.endUtc};
const moved=await c.request(`/api/appointments/${a.body.id}/reschedule`,move,crypto.randomUUID());assert.equal(moved.status,200,JSON.stringify(moved));assert.equal(moved.body.version,2);
assert.equal((await c.request(`/api/appointments/${a.body.id}/cancel`,{version:1},crypto.randomUUID())).body.code,'version_conflict');
const cancelled=await c.request(`/api/appointments/${a.body.id}/cancel`,{version:2},crypto.randomUUID());assert.equal(cancelled.body.status,'Cancelled');
console.log('PASS A05/A08/A12/A18 HTTP reschedule, stale version, cancellation and roles');
