// Distinct-user model: each VU acts as its own account, logs in lazily, refreshes via the
// refreshToken cookie (k6 keeps a cookie jar per VU) and re-logs in on 401.
import { fail, sleep } from 'k6';
import { json, post, get } from './api.js';

const ENV = __ENV;
export const POOL_PREFIX = ENV.LOAD_USER_PREFIX || 'ltpatient';
export const POOL_PASSWORD = ENV.LOAD_USER_PASSWORD || 'LoadTest#2026';
// Size of the provisioned pool (scripts/provision-load-users.mjs). 0 = use seeded patient1..patientN.
export const POOL_SIZE = Number(ENV.LOAD_USER_POOL || 0);
const SEED_PATIENTS = Number(ENV.SEED_PATIENT_COUNT || 30);
const SEED_PASSWORD = ENV.BENCHMARK_PATIENT_PASSWORD || 'Password123!';
const STAFF_PASSWORD = ENV.BENCHMARK_STAFF_PASSWORD || 'Password123!';
const RECEPTIONISTS = Number(ENV.SEED_RECEPTIONIST_COUNT || 5);
const DOCTORS = Number(ENV.SEED_DOCTOR_COUNT || 10);
// Refresh well before the 15-minute access-token lifetime.
const REFRESH_AFTER_MS = Number(ENV.TOKEN_REFRESH_AFTER_S || 600) * 1000;

export function poolUsername(index) {
  if (POOL_SIZE > 0) return `${POOL_PREFIX}${String((index % POOL_SIZE) + 1).padStart(5, '0')}`;
  return `patient${(index % SEED_PATIENTS) + 1}`;
}

export function patientCredentials(index) {
  return { username: poolUsername(index), password: POOL_SIZE > 0 ? POOL_PASSWORD : SEED_PASSWORD };
}

export function receptionistCredentials(index) {
  return { username: `receptionist${(index % RECEPTIONISTS) + 1}`, password: STAFF_PASSWORD };
}

export function doctorCredentials(index) {
  return { username: `doctor${(index % DOCTORS) + 1}`, password: STAFF_PASSWORD };
}

// Per-VU cache (each VU has its own JS runtime, so module state is VU-local).
const cache = {};

let lastLoginStatus = 0;
let lastRetryAfterMs = 0;

function retryAfterMs(res) {
  const seconds = Number(res.headers['Retry-After']);
  return Number.isFinite(seconds) && seconds > 0 ? seconds * 1000 : 0;
}

function loginRaw(credentials) {
  const res = post('/auth/login', { body: credentials, cls: 'login', name: 'auth-login', allowed: [200] });
  lastLoginStatus = res.status;
  lastRetryAfterMs = res.status === 429 || res.status === 503 ? retryAfterMs(res) : 0;
  if (res.status !== 200) return null;
  const body = json(res);
  return body?.accessToken || body?.token || null;
}

function refreshRaw() {
  const res = post('/auth/refresh', { cls: 'refresh', name: 'auth-refresh', allowed: [200, 401] });
  if (res.status !== 200) return null;
  const body = json(res);
  return body?.accessToken || body?.token || null;
}

// Client-side backoff after a failed login (2 s, 4 s, … capped at 60 s) like a real app, so an
// overloaded Identity service is not hammered by a retry storm from the load generator itself.
const backoff = {};

/** Returns a valid access token for the account, logging in or refreshing as a real client would. */
export function tokenFor(credentials) {
  const entry = cache[credentials.username];
  const now = Date.now();
  if (entry && now - entry.at < REFRESH_AFTER_MS) return entry.token;
  const b = backoff[credentials.username];
  if (!entry && b && now < b.until) return null;
  let token = entry ? refreshRaw() : null;
  if (!token) token = loginRaw(credentials);
  if (!token) {
    const failures = (b?.failures || 0) + 1;
    // Honour Retry-After from the gateway limiter or Identity load shedding (§5.3), else back off exponentially.
    const wait = Math.max(lastRetryAfterMs, Math.min(60000, 1000 * 2 ** failures));
    backoff[credentials.username] = { failures, until: now + wait };
    return null;
  }
  delete backoff[credentials.username];
  cache[credentials.username] = { token, at: now };
  return token;
}

/**
 * setup()-side session warm-up: logs the given accounts in at a paced rate (default 4/s, below Identity's
 * measured login capacity), honouring Retry-After on 429/503. Returns { tokens: {username: token}, issuedAt }.
 * Real users are mostly already signed in when a booking rush starts; warming keeps a spike test about
 * booking/browsing instead of an artificial "everyone logs in in the same second" event.
 */
export function warmSessions(credentialsList, { rate = Number(ENV.LOGIN_WARMUP_RATE || 4), maxAttempts = 6 } = {}) {
  const tokens = {};
  const unique = [...new Map(credentialsList.map((c) => [c.username, c])).values()];
  const interval = 1 / Math.max(0.1, rate);
  let failed = 0;
  for (const credentials of unique) {
    let token = null;
    for (let attempt = 1; attempt <= maxAttempts && !token; attempt++) {
      token = loginRaw(credentials);
      if (!token) {
        if (lastLoginStatus === 401 || lastLoginStatus === 403) break;
        sleep(Math.max(lastRetryAfterMs / 1000, interval * 2 ** attempt));
      }
    }
    if (token) tokens[credentials.username] = token;
    else failed++;
    sleep(interval);
  }
  if (failed > unique.length * 0.05) fail(`Session warm-up failed for ${failed}/${unique.length} accounts (last HTTP ${lastLoginStatus}).`);
  return { tokens, issuedAt: Date.now() };
}

/** VU-side: adopt tokens issued in setup() so the VU does not log in again until refresh is due. */
let adopted = false;
export function adoptSessions(warm) {
  if (!warm || adopted) return;
  for (const [username, token] of Object.entries(warm.tokens || {})) {
    if (!cache[username]) cache[username] = { token, at: warm.issuedAt };
  }
  adopted = true;
}

/** Drops a cached token after an unexpected 401 so the next call logs in again. */
export function invalidate(credentials) {
  delete cache[credentials.username];
}

/** Strict login for setup(): throws when credentials are wrong so misconfiguration fails fast. */
export function mustLogin(credentials) {
  const token = loginRaw(credentials);
  if (!token && lastLoginStatus === 429) {
    fail(`Login for ${credentials.username} was rate limited (429). The load generator uses one IP: raise AUTH_RATE_LIMIT_PERMIT_LIMIT for load runs (tests/load/README.md §3).`);
  }
  if (!token) fail(`Login failed for ${credentials.username} (HTTP ${lastLoginStatus}). Check seed data / provisioning.`);
  return token;
}

/** Patient profile id for the current pool user (needed for staff walk-in flows). */
export function patientIdFor(credentials) {
  const entry = cache[credentials.username];
  if (entry?.patientId) return entry.patientId;
  const token = tokenFor(credentials);
  if (!token) return null;
  const res = get('/patients/me', { token, name: 'patients-me', allowed: [200] });
  const id = json(res)?.id || null;
  if (id && cache[credentials.username]) cache[credentials.username].patientId = id;
  return id;
}
