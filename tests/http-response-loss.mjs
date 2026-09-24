import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { Client } from './auth.mjs';
const client=await new Client().login();
const base=process.env.API_URL??'http://127.0.0.1:5080';
const start=new Date(Date.UTC(2048,0,1)+Math.floor(Math.random()*100000)*900000);
const payload={patientId:1,resourceId:1,startUtc:start.toISOString(),endUtc:new Date(+start+900000).toISOString()};
const key=crypto.randomUUID();let saved,first=true;
const proxy=createServer(async(req,res)=>{
 try{
  const result=await client.request('/api/appointments',payload,key);
  assert.equal(result.status,200);saved??=result.body;
  if(first){first=false;req.socket.destroy();return;}
  res.setHeader('Content-Type','application/json');res.end(JSON.stringify(result.body));
 }catch(error){res.statusCode=500;res.end(JSON.stringify({error:error.message}));}
});
await new Promise(resolve=>proxy.listen(0,'127.0.0.1',resolve));
try{
 const url='http://127.0.0.1:'+proxy.address().port;
 await assert.rejects(()=>fetch(url,{method:'POST'}));
 const replay=await (await fetch(url,{method:'POST'})).json();assert.deepEqual(replay,saved);
 const detail=(await client.request('/api/appointments/'+replay.id)).body;assert.equal(detail.audit.length,1);assert.equal(detail.sync.length,1);
 console.log('PASS A11 actual proxy response loss after API success, identical replay and one persistent audit/outbox effect');
}finally{await new Promise(resolve=>proxy.close(resolve));}
