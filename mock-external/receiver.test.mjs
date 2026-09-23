import {fileURLToPath} from 'node:url';
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
import {mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
const token='isolated-test-token';
test('A14/A15 durable dedupe, response loss and out-of-order snapshots',async()=>{
 const dir=await mkdtemp(join(tmpdir(),'clinicflow-mock-'));let child,url;
 async function start(){child=spawn(process.execPath,[fileURLToPath(new URL('./server.mjs',import.meta.url))],{env:{...process.env,PORT:'0',MOCK_DB:join(dir,'db.sqlite'),INTEGRATION_TOKEN:token,MOCK_CONTROL:'true'},stdio:['ignore','pipe','pipe']});const [buffer]=await once(child.stdout,'data');url='http://127.0.0.1:'+buffer.toString().trim().split(' ').at(-1);}
 async function stop(){const ended=once(child,'exit');child.kill('SIGTERM');await ended;}
 async function request(path,payload){return fetch(url+path,{method:payload?'POST':'GET',headers:{'Content-Type':'application/json','X-Integration-Key':token},body:payload?JSON.stringify(payload):undefined});}
 function message(id,version,status){return {messageId:id,appointmentId:'a',version,snapshot:{id:'a',version,status}};}
 try{
  await start();assert.equal((await fetch(url+'/state')).status,401);
  await request('/control',{mode:'lose-response'});await assert.rejects(()=>request('/messages',message('first',1,'Pending')));
  await stop();await start();assert.equal((await (await request('/messages',message('first',1,'Pending'))).json()).duplicate,true);
  await request('/messages',message('new',3,'Cancelled'));await request('/messages',message('late',2,'Confirmed'));
  const state=await (await request('/state')).json();assert.equal(state.receipts.length,3);assert.equal(state.snapshots.length,1);assert.equal(state.snapshots[0].version,3);assert.equal(JSON.parse(state.snapshots[0].payload).status,'Cancelled');
  assert.equal((await request('/messages',{foo:'bar'})).status,400);
 }finally{if(child?.exitCode===null)await stop();await rm(dir,{recursive:true,force:true});}
});
