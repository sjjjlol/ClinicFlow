import {createServer} from 'node:http';
import {DatabaseSync} from 'node:sqlite';
const token=process.env.INTEGRATION_TOKEN;
if(!token)throw new Error('INTEGRATION_TOKEN required');
const db=new DatabaseSync(process.env.MOCK_DB??'/data/external.sqlite');
db.exec(`PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS receipts(message_id TEXT PRIMARY KEY,appointment_id TEXT NOT NULL,version INTEGER NOT NULL);CREATE TABLE IF NOT EXISTS snapshots(appointment_id TEXT PRIMARY KEY,version INTEGER NOT NULL,payload TEXT NOT NULL);`);
let mode=process.env.MOCK_MODE??'normal';
async function body(req){let text='';for await(const chunk of req){text+=chunk;if(text.length>65536)throw new Error('body too large');}return JSON.parse(text||'{}');}
const server=createServer(async(req,res)=>{
 function reply(code,value){res.writeHead(code,{'Content-Type':'application/json'});res.end(JSON.stringify(value));}
 if(req.url==='/health'){reply(200,{status:'ready'});return;}
 if(req.headers['x-integration-key']!==token){reply(401,{error:'unauthorized'});return;}
 try{
  if(process.env.MOCK_CONTROL==='true'&&req.url==='/control'&&req.method==='POST'){mode=(await body(req)).mode;reply(200,{mode});return;}
  if(req.url==='/state'&&req.method==='GET'){reply(200,{receipts:db.prepare('SELECT * FROM receipts').all(),snapshots:db.prepare('SELECT * FROM snapshots').all()});return;}
  if(req.url!=='/messages'||req.method!=='POST'){reply(404,{error:'not_found'});return;}
  if(mode==='unavailable'){reply(503,{error:'simulated_outage'});return;}
  if(mode==='permanent'){reply(422,{error:'simulated_rejection'});return;}
  const m=await body(req);
  if(typeof m.messageId!=='string'||m.messageId.length>36||typeof m.appointmentId!=='string'||!Number.isSafeInteger(m.version)||m.version<1||m.snapshot?.id!==m.appointmentId||m.snapshot?.version!==m.version||!['Pending','Confirmed','Cancelled'].includes(m.snapshot?.status)){reply(400,{error:'invalid_message'});return;}
  db.exec('BEGIN IMMEDIATE');let duplicate=false,applied=false;
  try{
   const receipt=db.prepare('INSERT OR IGNORE INTO receipts VALUES(?,?,?)').run(m.messageId,m.appointmentId,m.version);duplicate=receipt.changes===0;
   if(!duplicate){const update=db.prepare('INSERT INTO snapshots VALUES(?,?,?) ON CONFLICT(appointment_id) DO UPDATE SET version=excluded.version,payload=excluded.payload WHERE excluded.version>snapshots.version').run(m.appointmentId,m.version,JSON.stringify(m.snapshot));applied=update.changes>0;}
   db.exec('COMMIT');
  }catch(e){db.exec('ROLLBACK');throw e;}
  if(mode==='lose-response'){mode='normal';req.socket.destroy();return;}
  reply(200,{duplicate,applied});
 }catch{reply(400,{error:'invalid_request'});}
});
server.listen(Number(process.env.PORT??5090),'0.0.0.0',()=>console.log('Mock receiver ready '+server.address().port));
function stop(){server.close(()=>{db.close();process.exit(0);});}
process.on('SIGTERM',stop);process.on('SIGINT',stop);
