import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
const base=process.env.API_URL??'http://127.0.0.1:5080';
export class Client {
  cookies=new Map(); token='';
  async request(path,body,key,method){
    const res=await fetch(base+path,{method:method??(body===undefined?'GET':'POST'),headers:{'Content-Type':'application/json','Cookie':[...this.cookies].map(([k,v])=>`${k}=${v}`).join('; '),'X-CSRF-TOKEN':this.token,...(key?{'Idempotency-Key':key}:{})},body:body===undefined?undefined:JSON.stringify(body)});
    for(const cookie of res.headers.getSetCookie()){const [kv]=cookie.split(';'); const at=kv.indexOf('=');this.cookies.set(kv.slice(0,at),kv.slice(at+1));}
    const text=await res.text();return {status:res.status,body:text?JSON.parse(text):null,retryAfter:res.headers.get('retry-after')};
  }
  async csrf(){this.token=(await this.request('/api/auth/csrf')).body.token;}
  async login(username='scheduler'){
    await this.csrf();
    for(let attempt=0;attempt<2;attempt++){
      const r=await this.request('/api/auth/login',{username,password:process.env.DEMO_PASSWORD});
      if(r.status===429 && attempt===0){
        const seconds=Number(r.retryAfter??60);
        await new Promise(resolve=>setTimeout(resolve,(Number.isFinite(seconds)?Math.min(60,Math.max(0,seconds)):60)*1000+100));
        continue;
      }
      assert.equal(r.status,200);await this.csrf();return this;
    }
    throw new Error('Login retry exhausted');
  }
}
if(fileURLToPath(import.meta.url)===process.argv[1]){
 const c=new Client();assert.equal((await c.request('/api/ois/workspace')).status,401);
 assert.equal((await c.request('/api/auth/login',{username:'scheduler',password:'wrong'})).status,400);
 await c.csrf();assert.equal((await c.request('/api/auth/login',{username:'scheduler',password:'wrong'})).status,401);
 for(const role of ['scheduler','clinician','physicist']){await c.login(role);assert.equal((await c.request('/api/auth/me')).body.name,role);assert.ok((await c.request('/api/ois/workspace')).body.patients.length>=2);await c.request('/api/auth/logout',{});assert.equal((await c.request('/api/auth/me')).status,401);}
 for(const role of ['admin','taskoperator']){await c.csrf();assert.equal((await c.request('/api/auth/login',{username:role,password:process.env.DEMO_PASSWORD})).status,401);}
 console.log('PASS OIS authentication: three roles, password, CSRF, workspace, logout');
}
