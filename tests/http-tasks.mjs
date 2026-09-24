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
for(const path of ['/api/appointments',...['reschedule','cancel','confirm','complete-task'].map(action=>`/api/appointments/${a.id}/${action}`),'/api/sync/unknown/retry'])assert.equal((await new Client().request(path,{},crypto.randomUUID())).status,401);
const page1=(await s.request('/api/appointments?size=1&page=1')).body,page2=(await s.request('/api/appointments?size=1&page=2')).body;
assert.notEqual(page1.items[0].id,page2.items[0].id);assert.ok(Date.parse(page1.items[0].startUtc)>=Date.parse(page2.items[0].startUtc));
const confirmedList=(await s.request('/api/appointments?status=Confirmed&resourceId=1')).body;assert.ok(confirmedList.items.every(x=>x.status==='Confirmed'&&x.resourceId===1));
assert.equal((await s.request('/api/appointments?size=999&page=2147483647')).body.size,100);
console.log('PASS A18/A19 all unauthenticated writes, stable pagination, filters and page bounds');
