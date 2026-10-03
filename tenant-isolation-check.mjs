#!/usr/bin/env node
/*
 * Tenant isolation check for the QR Menu API.
 *
 * Creates TWO throw-away restaurants (A and B), puts data in A, then acts as restaurant B
 * (and as an anonymous / tampered caller) and tries to read, change or delete A's data.
 * Every attack must be refused. Exit code 0 = all good, 1 = something leaked.
 *
 * Usage:   node tenant-isolation-check.mjs [apiBaseUrl]
 * Default: http://localhost:5176   (the "http" profile in launchSettings.json)
 *
 * Run it against your DEV database only: the two test restaurants stay behind
 * (names start with "Isolation A/B"). Delete them from the DB whenever you like.
 */

const BASE = (process.argv[2] ?? 'http://localhost:5176').replace(/\/$/, '');
const suffix = Date.now().toString(36);
const results = [];

async function call(method, path, { token, body } = {}) {
  const res = await fetch(BASE + path, {
    method,
    headers: {
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {})
    },
    body: body !== undefined ? JSON.stringify(body) : undefined
  });
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* not JSON */ }
  return { status: res.status, json, text };
}

function check(name, ok, detail = '') {
  results.push({ name, ok });
  console.log(`${ok ? '  PASS' : '  FAIL'}  ${name}${ok ? '' : detail ? `  -> ${detail}` : ''}`);
}

/** The attack must be refused: 404 (not yours / doesn't exist for you) or 401/403. */
function refused(name, res) {
  check(name, [401, 403, 404].includes(res.status), `got HTTP ${res.status}: ${res.text.slice(0, 120)}`);
}

function tamper(token, patch) {
  const [h, p, s] = token.split('.');
  const payload = JSON.parse(Buffer.from(p, 'base64url').toString());
  Object.assign(payload, patch);
  return [h, Buffer.from(JSON.stringify(payload)).toString('base64url'), s].join('.');
}

async function register(label) {
  const res = await call('POST', '/api/auth/register', {
    body: {
      restaurantName: `Isolation ${label} ${suffix}`,
      ownerName: `Isolation ${label}`,
      email: `isolation-${label.toLowerCase()}-${suffix}@example.com`,
      password: 'Isolate#12345',
      whatsAppNumber: '9876543210'
    }
  });
  if (res.status !== 200 && res.status !== 201) {
    console.error(`Could not register restaurant ${label}: HTTP ${res.status} ${res.text}`);
    process.exit(2);
  }
  return res.json;
}

const itemBody = (categoryId, name) => ({
  categoryId, name, description: null, price: 100, imageUrl: null, isVeg: true, tag: null, variants: null, addOns: null
});

console.log(`\nTenant isolation check against ${BASE}\n`);

// ---------- setup ----------
const A = await register('A');
const B = await register('B');
const ta = A.token, tb = B.token;
console.log(`  Restaurant A: ${A.restaurantSlug}   Restaurant B: ${B.restaurantSlug}\n`);

const catA = (await call('POST', '/api/categories', { token: ta, body: { name: 'A-Secret-Category' } })).json;
const tableA = (await call('POST', '/api/tables', { token: ta, body: { number: 'A1', capacity: 2 } })).json;
const itemA = (await call('POST', '/api/items', { token: ta, body: itemBody(catA.id, 'A-Secret-Item') })).json;
const catB = (await call('POST', '/api/categories', { token: tb, body: { name: 'B-Category' } })).json;
const itemB = (await call('POST', '/api/items', { token: tb, body: itemBody(catB.id, 'B-Item') })).json;
const bgA = (await call('POST', '/api/backgrounds', { token: ta, body: { imageUrl: '/uploads/a-secret.jpg' } })).json;

const orderRes = await call('POST', `/api/public/${A.restaurantSlug}/orders`, {
  body: { tableNumber: 'A1', customerName: 'Test', customerPhone: '9999999999', note: null, skipServiceCharge: false,
          items: [{ menuItemId: itemA.id, variantId: null, addOnIds: null, qty: 1 }] }
});
const orderA = orderRes.json;
const reqRes = await call('POST', `/api/public/${A.restaurantSlug}/requests`, { body: { tableNumber: 'A1', type: 'Water' } });
const requestA = reqRes.json;

if (!orderA?.id) console.log(`  (note) could not place a test order at A (HTTP ${orderRes.status}: ${orderRes.text.slice(0, 100)}) - order checks skipped`);
if (!requestA?.id) console.log(`  (note) could not create a test service request at A (HTTP ${reqRes.status}) - request checks skipped`);

// ---------- positive controls: A can use its own data ----------
console.log('Positive controls (A can reach its own data)');
const aCats = await call('GET', '/api/categories', { token: ta });
check('A sees its own category', aCats.status === 200 && JSON.stringify(aCats.json).includes(catA.id), `HTTP ${aCats.status}`);
const aMe = await call('GET', '/api/restaurant', { token: ta });
check('A sees its own restaurant', aMe.status === 200 && aMe.json?.id === A.restaurantId, `HTTP ${aMe.status}`);

// ---------- B attacks A's data by ID ----------
console.log('\nRestaurant B tries A\'s records by ID (must all be refused)');
refused('B cannot rename A\'s category', await call('PUT', `/api/categories/${catA.id}`, { token: tb, body: { name: 'hacked' } }));
refused('B cannot delete A\'s category', await call('DELETE', `/api/categories/${catA.id}`, { token: tb }));
refused('B cannot edit A\'s item', await call('PUT', `/api/items/${itemA.id}`, { token: tb, body: itemBody(catB.id, 'hacked') }));
refused('B cannot delete A\'s item', await call('DELETE', `/api/items/${itemA.id}`, { token: tb }));
refused('B cannot toggle A\'s item availability', await call('PATCH', `/api/items/${itemA.id}/availability`, { token: tb, body: { isAvailable: false } }));
refused('B cannot create an item inside A\'s category', await call('POST', '/api/items', { token: tb, body: itemBody(catA.id, 'planted') }));
refused('B cannot move its item into A\'s category', await call('PUT', `/api/items/${itemB.id}`, { token: tb, body: itemBody(catA.id, 'moved') }));
refused('B cannot edit A\'s table', await call('PUT', `/api/tables/${tableA.id}`, { token: tb, body: { number: 'X', capacity: 1, isActive: true } }));
refused('B cannot delete A\'s table', await call('DELETE', `/api/tables/${tableA.id}`, { token: tb }));
refused('B cannot change A\'s background', await call('PUT', `/api/backgrounds/${bgA.id}`, { token: tb, body: { slots: 1, isDefault: true } }));
refused('B cannot delete A\'s background', await call('DELETE', `/api/backgrounds/${bgA.id}`, { token: tb }));
refused('B cannot download A\'s QR cards', await call('GET', `/api/qr/${A.restaurantSlug}`, { token: tb }));
if (orderA?.id) {
  refused('B cannot read A\'s order', await call('GET', `/api/orders/${orderA.id}`, { token: tb }));
  refused('B cannot change A\'s order status', await call('PATCH', `/api/orders/${orderA.id}/status`, { token: tb, body: { status: 'Cancelled' } }));
}
if (requestA?.id) {
  refused('B cannot complete A\'s service request', await call('POST', `/api/requests/${requestA.id}/complete`, { token: tb }));
}

// ---------- B's lists must not contain A's data ----------
console.log('\nRestaurant B\'s lists must not contain any of A\'s data');
for (const path of ['/api/categories', '/api/items', '/api/tables', '/api/orders', '/api/backgrounds', '/api/requests']) {
  const res = await call('GET', path, { token: tb });
  const body = res.text;
  const leaked = body.includes('A-Secret') || body.includes(catA.id) || body.includes(itemA.id) || body.includes(tableA.id)
    || (orderA?.id && body.includes(orderA.id)) || (requestA?.id && body.includes(requestA.id)) || body.includes('a-secret.jpg');
  check(`B's ${path} has none of A's data`, res.status === 200 && !leaked, leaked ? 'A\'s data found in response!' : `HTTP ${res.status}`);
}
const bMe = await call('GET', '/api/restaurant', { token: tb });
check('B\'s restaurant endpoint returns B (never A)', bMe.status === 200 && bMe.json?.id === B.restaurantId && bMe.json?.id !== A.restaurantId, `HTTP ${bMe.status}`);

// ---------- public (customer) side ----------
console.log('\nCustomer side: using B\'s menu link with A\'s IDs');
if (orderA?.id) {
  refused('B\'s link cannot show A\'s order', await call('GET', `/api/public/${B.restaurantSlug}/orders/${orderA.id}`));
  refused('B\'s link cannot cancel A\'s order', await call('PATCH', `/api/public/${B.restaurantSlug}/orders/${orderA.id}/cancel`));
}
const bMenu = await call('GET', `/api/public/${B.restaurantSlug}/menu`);
check('B\'s public menu has none of A\'s items', bMenu.status === 200 && !bMenu.text.includes('A-Secret') && !bMenu.text.includes(itemA.id), `HTTP ${bMenu.status}`);
refused('Cannot order A\'s item through B\'s link', await call('POST', `/api/public/${B.restaurantSlug}/orders`, {
  body: { tableNumber: null, customerName: 'x', customerPhone: '9999999999', note: null, skipServiceCharge: false,
          items: [{ menuItemId: itemA.id, variantId: null, addOnIds: null, qty: 1 }] }
}));
refused('Cannot raise a request for A\'s table through B\'s link', await call('POST', `/api/public/${B.restaurantSlug}/requests`, { body: { tableNumber: 'A1', type: 'Water' } }));

// ---------- no token / tampered token ----------
console.log('\nNo token and tampered token');
for (const path of ['/api/restaurant', '/api/orders', '/api/categories', '/api/tables', '/api/items']) {
  const res = await call('GET', path);
  check(`no token -> ${path} is 401`, res.status === 401, `got HTTP ${res.status}`);
}
const forged = tamper(ta, { restaurantId: B.restaurantId });
const forgedRes = await call('GET', '/api/restaurant', { token: forged });
check('token with edited restaurantId is rejected (401)', forgedRes.status === 401, `got HTTP ${forgedRes.status}`);
const garbage = await call('GET', '/api/restaurant', { token: 'not.a.token' });
check('garbage token is rejected (401)', garbage.status === 401, `got HTTP ${garbage.status}`);

// ---------- summary ----------
const failed = results.filter((r) => !r.ok);
console.log(`\n${results.length - failed.length}/${results.length} checks passed.`);
if (failed.length) {
  console.log('\nFAILED:\n' + failed.map((f) => `  - ${f.name}`).join('\n'));
  process.exit(1);
}
console.log('No cross-restaurant access found.');
