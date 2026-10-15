import assert from 'node:assert/strict';
const base = process.env.API_URL ?? 'http://127.0.0.1:5080';
const home = await fetch(base + '/');
assert.equal(home.status, 200, 'Bundled OIS homepage must load');
const html = await home.text();
assert.ok(html.includes('ClinicFlow'));
const assets = [...html.matchAll(/(?:src|href)="(\/assets\/[^\"]+)"/g)].map(m => m[1]);
assert.ok(assets.length >= 2, 'Bundled JavaScript and CSS must be present');
for (const path of assets) assert.equal((await fetch(base + path)).status, 200, path);
for (const path of ['/legacy', '/legacy/appointments', '/fhir/r4/metadata', '/unknown'])
  assert.equal((await fetch(base + path)).status, 404, path);
console.log('PASS bundled OIS UI: homepage/assets load, retired and unknown paths return 404');
