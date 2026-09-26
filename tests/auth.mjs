import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
const base=process.env.API_URL??'http://127.0.0.1:5080';
export class Client {
  cookies=new Map(); token='';
  async request(path,body,key){
    const res=await fetch(base+path,{method:body===undefined?'GET':'POST',headers:{'Content-Type':'application/json','Cookie':[...this.cookies].map(([k,v])=>`${k}=${v}`).join('; '),'X-CSRF-TOKEN':this.token,...(key?{'Idempotency-Key':key}:{})},body:body===undefined?undefined:JSON.stringify(body)});
    for(const cookie of res.headers.getSetCookie()){const [kv]=cookie.split(';'); const at=kv.indexOf('=');this.cookies.set(kv.slice(0,at),kv.slice(at+1));}
    const text=await res.text();return {status:res.status,body:text?JSON.parse(text):null};
  }
  async csrf(){this.token=(await this.request('/api/auth/csrf')).body.token;}
  async login(username='scheduler'){await this.csrf();const r=await this.request('/api/auth/login',{username,password:process.env.DEMO_PASSWORD});assert.equal(r.status,200);await this.csrf();return this;}
}
if(fileURLToPath(import.meta.url)===process.argv[1]){
 const c=new Client();assert.equal((await c.request('/api/patients')).status,401);
 assert.equal((await c.request('/api/auth/login',{username:'scheduler',password:'wrong'})).status,400);
 await c.csrf();assert.equal((await c.request('/api/auth/login',{username:'scheduler',password:'wrong'})).status,401);
 for(const role of ['scheduler','taskoperator','admin']){await c.login(role);assert.equal((await c.request('/api/auth/me')).body.name,role);assert.ok((await c.request('/api/patients')).body.length>=2);assert.equal((await c.request('/api/resources')).body.length,2);await c.request('/api/auth/logout',{});assert.equal((await c.request('/api/auth/me')).status,401);}
 console.log('PASS A01/A18: three roles, password, CSRF, catalog, logout');
}
