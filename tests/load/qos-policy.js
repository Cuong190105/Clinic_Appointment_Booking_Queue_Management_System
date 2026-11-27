// Live verification of the Gateway QoS policy (SLI_SLO_MATRIX §5.1, §5.3) against a running stack.
// Run with the DEFAULT limits (do not apply the load-test overrides) and wait >= 60 s after other
// tests so the fixed windows are fresh. Single VU, sequential, deterministic.
import { check, fail } from 'k6';
import http from 'k6/http';
import { Counter, Rate } from 'k6/metrics';
import { BASE_URL, DEFAULT_HEADERS } from './config.js';
import { uuid } from './lib/api.js';
import { mustLogin, patientCredentials, receptionistCredentials } from './lib/session.js';
import { summarize } from './lib/summary.js';

const USER_LIMIT = Number(__ENV.QOS_USER_LIMIT || 100);
const STAFF_LIMIT = Number(__ENV.QOS_STAFF_LIMIT || 600);
const AUTH_LIMIT = Number(__ENV.QOS_AUTH_LIMIT || 10);
const BOOKING_LIMIT = Number(__ENV.QOS_BOOKING_LIMIT || 10);
const policyPassed = new Rate('qos_policy_checks');
const checksRun = new Counter('qos_policy_checks_run');

export const options = {
  scenarios: { qos: { executor: 'per-vu-iterations', vus: 1, iterations: 1, maxDuration: '5m' } },
  // Both gates: every policy check passed AND checks actually ran (a 0-sample Rate would pass vacuously).
  thresholds: { qos_policy_checks: ['rate==1'], qos_policy_checks_run: ['count>=9'] },
};

function send(method, path, token, body, extraHeaders = {}) {
  const headers = { ...DEFAULT_HEADERS, ...extraHeaders };
  if (token) headers.Authorization = `Bearer ${token}`;
  return http.request(method, `${BASE_URL}${path}`, body === undefined ? null : JSON.stringify(body), {
    headers, responseCallback: http.expectedStatuses({ min: 200, max: 499 }), tags: { endpoint: path },
  });
}

/** Sends up to `max` requests; returns the 1-based index of the first 429 (or 0) and that response. */
function firstThrottle(max, fn) {
  for (let i = 1; i <= max; i++) {
    const res = fn(i);
    if (res.status === 429) return { at: i, res };
    if (res.status >= 500) fail(`Unexpected ${res.status} while probing rate limits.`);
  }
  return { at: 0, res: null };
}

function verify(name, condition) {
  checksRun.add(1);
  policyPassed.add(condition);
  check(condition, { [name]: (v) => v === true });
}

export function setup() {
  // Two logins happen here and count toward this IP's auth quota; the auth check accounts for it.
  const patient = mustLogin(patientCredentials(Number(__ENV.QOS_PATIENT_INDEX || 25)));
  // Staff account is optional so the policy can be checked on an environment without staff seed data.
  const staffLogin = send('POST', '/auth/login', null, receptionistCredentials(4));
  let receptionist = null;
  try { receptionist = staffLogin.status === 200 ? (JSON.parse(staffLogin.body).result || JSON.parse(staffLogin.body)).accessToken : null; } catch { receptionist = null; }
  if (!receptionist) console.warn('[QOS] receptionist login failed; staff-tier check will be skipped.');
  return { patient, receptionist };
}

export default function (data) {
  // 1. Correlation ID (§5.1): a valid GUID is preserved; anything else is replaced by a new GUID.
  const cid = uuid();
  const echoed = send('GET', '/specialties', null, undefined, { 'X-Correlation-ID': cid });
  const echoedId = echoed.headers['X-Correlation-Id'] || echoed.headers['X-Correlation-ID'];
  verify('client GUID X-Correlation-ID is preserved', echoedId === cid);
  const replaced = send('GET', '/specialties', null, undefined, { 'X-Correlation-ID': 'not-a-guid' });
  const replacedId = replaced.headers['X-Correlation-Id'] || replaced.headers['X-Correlation-ID'];
  verify('non-GUID X-Correlation-ID is replaced by a server GUID', /^[0-9a-f-]{36}$/i.test(replacedId || '') && replacedId !== 'not-a-guid');

  // 2. Exempt operational endpoints are never throttled.
  const exempt = firstThrottle(USER_LIMIT + 20, () => send('GET', '/healthz'));
  verify('/healthz is exempt from rate limiting', exempt.at === 0);

  // 3. Booking creation: BOOKING_LIMIT per patient per minute (requests target a non-existent schedule, so nothing is booked).
  const booking = firstThrottle(BOOKING_LIMIT + 2, (i) => send('POST', '/appointments', data.patient,
    { workScheduleId: '00000000-0000-0000-0000-000000000001', timeSlot: '09:00:00', reason: `qos probe ${i}` },
    { 'Idempotency-Key': `qos-${Date.now()}-${i}` }));
  verify(`booking limit throttles at request ${BOOKING_LIMIT + 1}`, booking.at === BOOKING_LIMIT + 1);
  verify('booking 429 carries Retry-After', Boolean(booking.res?.headers['Retry-After']));

  // 4. General patient quota: the same patient is throttled once USER_LIMIT is used
  //    (booking attempts above already consumed BOOKING_LIMIT + 1 permits of this window).
  const used = BOOKING_LIMIT + 1;
  const general = firstThrottle(USER_LIMIT + 5, () => send('GET', '/me/appointments', data.patient));
  verify(`patient quota throttles after ${USER_LIMIT} requests/min`, general.at > 0 && general.at <= USER_LIMIT - used + 2);
  const retryAfter = Number(general.res?.headers['Retry-After'] || 0);
  verify('429 Retry-After is within the 60 s window', retryAfter >= 1 && retryAfter <= 60);
  let envelopeOk = false;
  try { envelopeOk = JSON.parse(general.res.body).isSuccess === false; } catch { envelopeOk = false; }
  verify('429 uses the ApiResponse envelope', envelopeOk);

  // 5. Staff tier: a receptionist can exceed the patient quota without being throttled.
  if (data.receptionist) {
    const staff = firstThrottle(Math.min(STAFF_LIMIT - 10, USER_LIMIT + 50), () => send('GET', '/receptionist/appointments/pending', data.receptionist));
    verify(`staff tier allows more than ${USER_LIMIT} requests/min`, staff.at === 0);
  }

  // 6. Auth routes: AUTH_LIMIT per IP per minute (setup already used 2 permits).
  const auth = firstThrottle(AUTH_LIMIT + 2, () => send('POST', '/auth/login', null, { username: 'qos-nonexistent-user', password: 'wrong-password-123' }));
  verify(`login is throttled per IP within ${AUTH_LIMIT} attempts/min`, auth.at > 0 && auth.at <= AUTH_LIMIT);
}

export function handleSummary(data) {
  return summarize('qos-policy', data, { limits: { user: USER_LIMIT, staff: STAFF_LIMIT, auth: AUTH_LIMIT, booking: BOOKING_LIMIT } });
}
