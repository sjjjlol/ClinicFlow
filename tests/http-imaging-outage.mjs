// Controlled local-container fault exercise. Always restart Orthanc in finally.
import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import {readFile} from 'node:fs/promises';
import {Client} from './auth.mjs';
const client = await new Client().login();
const [study] = JSON.parse(await readFile('artifacts/dicom-fixtures/manifest.json','utf8'));
const start = new Date(Date.UTC(2047,0,1)+Math.floor(Math.random()*100000)*900000);
const a = await client.request('/api/appointments',{patientId:1,resourceId:1,startUtc:start.toISOString(),endUtc:new Date(+start+900000).toISOString()},crypto.randomUUID());
assert.equal(a.status,200);
const root=`/api/imaging/${a.body.id}`;
assert.equal((await client.request(root+'/links',{studyInstanceUid:study.studyInstanceUid})).status,204);
try {
  execFileSync('docker',['compose','--profile','imaging','stop','orthanc'],{stdio:'pipe',timeout:60000});
  for (const path of ['/search',`/studies/${study.studyInstanceUid}/metadata`]) {
    const failed = await client.request(root+path);
    assert.equal(failed.status,503,JSON.stringify(failed));
    assert.equal(failed.body.code,'imaging_unavailable');
    assert.ok(failed.body.correlationId);
    console.log('Expected outage response:',failed.body.code,'correlationId='+failed.body.correlationId);
  }
  assert.equal((await client.request(root+'/')).body.links.length,1);
  assert.equal((await client.request(root+`/links/${study.studyInstanceUid}/remove`,{})).status,204);
  assert.equal((await client.request(root+'/')).body.audit.length,2);
} finally {
  execFileSync('docker',['compose','--profile','imaging','up','-d','--wait','orthanc'],{stdio:'pipe',timeout:60000});
}
assert.equal((await client.request(root+'/search')).status,200);
console.log('PASS imaging outage: 503 with correlation, local unlink works, recovery succeeds');
