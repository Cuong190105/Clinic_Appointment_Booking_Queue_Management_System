#!/usr/bin/env node
// Provisions a pool of distinct patient accounts for realistic load tests (tests/load/lib/session.js).
// Idempotent: existing accounts are verified by logging in. Honours 429 Retry-After, so it also works
// with the default auth limit (slowly); raise AUTH_RATE_LIMIT_PERMIT_LIMIT temporarily for speed.
//
//   node scripts/provision-load-users.mjs --count 1500 [--base http://localhost:5000] [--concurrency 4]
//
// Accounts: <prefix>00001 … (default prefix "ltpatient", password from LOAD_USER_PASSWORD).
const args = Object.fromEntries(process.argv.slice(2).reduce((acc, cur, i, all) => {
  if (cur.startsWith('--')) acc.push([cur.slice(2), all[i + 1]?.startsWith('--') || all[i + 1] === undefined ? 'true' : all[i + 1]]);
  return acc;
}, []));

const BASE = (args.base || process.env.BASE_URL || 'http://localhost:5000').replace(/\/$/, '');
const COUNT = Number(args.count || process.env.LOAD_USER_POOL || 500);
const START = Number(args.start || 1);
const CONCURRENCY = Number(args.concurrency || 4);
const PREFIX = args.prefix || process.env.LOAD_USER_PREFIX || 'ltpatient';
const PASSWORD = process.env.LOAD_USER_PASSWORD || 'LoadTest#2026';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function postJson(path, body) {
  for (let attempt = 0; attempt < 20; attempt++) {
    const res = await fetch(`${BASE}${path}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
    if (res.status !== 429 && res.status < 500) return res;
    const wait = Number(res.headers.get('retry-after') || 5) * 1000;
    await sleep(res.status === 429 ? wait : 2000);
  }
  throw new Error(`${path} kept failing (rate limited or 5xx).`);
}

async function provision(n) {
  const username = `${PREFIX}${String(n).padStart(5, '0')}`;
  const login = await postJson('/auth/login', { username, password: PASSWORD });
  if (login.status === 200) return 'existing';
  const reg = await postJson('/auth/register', {
    username,
    password: PASSWORD,
    fullName: `Load Test Patient ${n}`,
    email: `${username}@loadtest.invalid`,
    phoneNumber: `09${String(10000000 + n).slice(-8)}`,
    dateOfBirth: '1990-01-01',
    gender: n % 2 === 0 ? 'Female' : 'Male',
    address: 'Load test fixture',
  });
  if (reg.status !== 200 && reg.status !== 201) {
    throw new Error(`register ${username} -> HTTP ${reg.status}: ${(await reg.text()).slice(0, 200)}`);
  }
  return 'created';
}

const stats = { created: 0, existing: 0, failed: 0 };
let next = START;
const end = START + COUNT - 1;
const started = Date.now();

async function worker() {
  while (next <= end) {
    const n = next++;
    try {
      stats[await provision(n)]++;
    } catch (error) {
      stats.failed++;
      console.error(error.message);
    }
    const done = stats.created + stats.existing + stats.failed;
    if (done % 50 === 0) console.log(`  ${done}/${COUNT} (${JSON.stringify(stats)}) ${(Date.now() - started) / 1000}s`);
  }
}

console.log(`Provisioning ${COUNT} accounts ${PREFIX}${String(START).padStart(5, '0')}…${PREFIX}${String(end).padStart(5, '0')} via ${BASE}`);
await Promise.all(Array.from({ length: CONCURRENCY }, worker));
console.log(`Done: ${JSON.stringify(stats)} in ${((Date.now() - started) / 1000).toFixed(1)}s`);
console.log(`Use with: LOAD_USER_POOL=${end} LOAD_USER_PREFIX=${PREFIX}`);
process.exit(stats.failed > 0 ? 1 : 0);
