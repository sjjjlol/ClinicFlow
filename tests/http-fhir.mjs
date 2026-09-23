import assert from 'node:assert/strict';
import {Client} from './auth.mjs';
const c=await new Client().login();assert.equal((await new Client().request('/fhir/r4/Patient/1')).status,401);
const metadata=(await c.request('/fhir/r4/metadata')).body;assert.equal(metadata.fhirVersion,'4.0.1');assert.deepEqual(metadata.rest[0].resource.map(r=>r.type),['Patient','Appointment']);
assert.equal((await c.request('/fhir/r4/Patient/1')).body.resourceType,'Patient');
assert.equal((await c.request('/fhir/r4/Patient/nope')).status,400);assert.equal((await c.request('/fhir/r4/Patient/99999')).status,404);assert.equal((await c.request('/fhir/r4/Patient/1?name=x')).body.resourceType,'OperationOutcome');
assert.equal((await c.request('/fhir/r4/Patient/1',{})).status,405);
const a=(await c.request('/api/appointments?size=1')).body.items[0];assert.equal((await c.request('/fhir/r4/Appointment/'+a.id)).body.resourceType,'Appointment');
console.log('PASS A20 FHIR R4 reads, capability statement, invalid/unsupported input and authentication');
