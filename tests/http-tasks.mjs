import assert from 'node:assert/strict';
import {Client} from './auth.mjs';
const s=await new Client().login(), t=await new Client().login('taskoperator'),admin=await new Client().login('admin');
const start=new Date(Date.UTC(2038,0,1)+Math.floor(Math.random()*100000)*900000);
let a=(await s.request('/api/appointments',{patientId:1,resourceId:1,startUtc:start.toISOString(),endUtc:new Date(+start+3600000).toISOString()},crypto.randomUUID())).body;
assert.equal((await s.request(`/api/appointments/${a.id}/confirm`,{version:a.version},crypto.randomUUID())).body.code,'prerequisites_incomplete');
const detail=(await s.request(`/api/appointments/${a.id}`)).body;
for(const task of detail.tasks){
 for(const user of [s,admin])assert.equal((await user.request(`/api/appointments/${a.id}/complete-task`,{version:a.version,taskId:task.id},crypto.randomUUID())).status,403);
 a=(await t.request(`/api/appointments/${a.id}/complete-task`,{version:a.version,taskId:task.id},crypto.randomUUID())).body;
}
for(const user of [t,admin])assert.equal((await user.request(`/api/appointments/${a.id}/confirm`,{version:a.version},crypto.randomUUID())).status,403);
a=(await s.request(`/api/appointments/${a.id}/confirm`,{version:a.version},crypto.randomUUID())).body;assert.equal(a.status,'Confirmed');
console.log('PASS A09/A18 tasks, confirmation preconditions and exact role policies');
