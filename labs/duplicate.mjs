import {createServer} from 'node:http';
import {DatabaseSync} from 'node:sqlite';
import {mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
const fixed=process.argv.includes('--fixed'),dir=await mkdtemp(join(tmpdir(),'clinicflow-L2-'));
const db=new DatabaseSync(join(dir,'lab.sqlite'));
db.exec('CREATE TABLE effects(id INTEGER PRIMARY KEY,message TEXT);CREATE TABLE receipts(message TEXT PRIMARY KEY);');
let loseResponse=true;
const server=createServer(async(req,res)=>{
 let text='';for await(const chunk of req)text+=chunk;const {messageId}=JSON.parse(text);
 db.exec('BEGIN IMMEDIATE');
 try{
  const apply=!fixed||db.prepare('INSERT OR IGNORE INTO receipts VALUES(?)').run(messageId).changes===1;
  if(apply)db.prepare('INSERT INTO effects(message) VALUES(?)').run(messageId);
  db.exec('COMMIT');
 }catch(e){db.exec('ROLLBACK');throw e;}
 if(loseResponse){loseResponse=false;req.socket.destroy();return;}
 res.end('ok');
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
try{
 const url='http://127.0.0.1:'+server.address().port;
 for(let i=0;i<2;i++){try{await fetch(url,{method:'POST',body:JSON.stringify({messageId:'same-message'})});}catch{console.log('First response lost after commit; caller retries same identity.');}}
 const count=db.prepare('SELECT COUNT(*) n FROM effects').get().n;console.log(`L2 fixed=${fixed}; deliveries=2; durable side effects=${count}`);
 if(count!==(fixed?1:2))throw new Error('Unexpected duplicate result');
}finally{await new Promise(resolve=>server.close(resolve));db.close();await rm(dir,{recursive:true,force:true});}
