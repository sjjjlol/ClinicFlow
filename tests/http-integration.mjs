import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import {Client} from './auth.mjs';
const scheduler=await new Client().login(), admin=await new Client().login('admin');
assert.equal((await scheduler.request('/api/sync')).status,403);
assert.equal((await scheduler.request('/api/sync/missing/retry',{},crypto.randomUUID())).status,403);
const start=new Date(Date.UTC(2045,0,1)+Math.floor(Math.random()*100000)*900000);
async function waitFor(get,predicate){const deadline=Date.now()+30000;while(Date.now()<deadline){const value=await get();if(predicate(value))return value;await new Promise(r=>setTimeout(r,300));}throw new Error('Timed out waiting for integration state');}
try{
 execFileSync('docker',['compose','stop','mock'],{stdio:'ignore'});
 const created=await scheduler.request('/api/appointments',{patientId:1,resourceId:1,startUtc:start.toISOString(),endUtc:new Date(+start+900000).toISOString()},crypto.randomUUID());assert.equal(created.status,200);const id=created.body.id;
 const get=async()=>(await scheduler.request('/api/appointments/'+id)).body;
 const pending=await waitFor(get,d=>d.sync[0].lastError);assert.equal(pending.appointment.status,'Pending');assert.equal(pending.sync[0].status,'Pending');
 execFileSync('docker',['compose','start','mock'],{stdio:'ignore'});
 const delivered=await waitFor(get,d=>d.sync[0].status==='Delivered');assert.equal(delivered.sync[0].id,pending.sync[0].id);
 const attempts=(await admin.request('/api/sync/'+pending.sync[0].id+'/attempts')).body;assert.ok(attempts.length>=2);
 const remote=await (await fetch('http://127.0.0.1:5090/state',{headers:{'X-Integration-Key':process.env.INTEGRATION_TOKEN}})).json();assert.equal(remote.receipts.filter(r=>r.message_id===pending.sync[0].id).length,1);
 console.log('PASS A13 real Docker outage/recovery, unchanged local appointment, same message delivered once, attempt history');
}finally{execFileSync('docker',['compose','start','mock'],{stdio:'ignore'});}
