// Login capacity and overload behaviour (SLI_SLO_MATRIX §2.3 "Identity capacity", §5.3).
//
// Open-model ramp of logins by distinct accounts, from LOGIN_START_RATE up to LOGIN_MAX_RATE per second.
// Password verification is CPU-bound (BCrypt), so above Identity's capacity some logins MUST fail. The
// SLO is about how they fail:
//   - accepted logins stay within the login latency SLO (p95 <= 1 s),
//   - excess logins are shed quickly with 503 + Retry-After (X-Clinic-Load-Shed), never by timing out (504),
//   - no other server errors.
// The summary reports the sustained accepted-login throughput per stage = Identity's measured capacity.
//
// Needs the per-IP auth limits raised (all traffic comes from one load-generator IP), e.g.
//   AUTH_RATE_LIMIT_PERMIT_LIMIT=100000 IDENTITY_AUTH_RATE_LIMIT_PERMIT_LIMIT=100000 docker compose up -d
// Accounts are reused round-robin from the seed or LOAD_USER_POOL; reuse does not change BCrypt cost.
import http from 'k6/http';
import { check, fail } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { BASE_URL, DEFAULT_HEADERS } from './config.js';
import { SLO } from './lib/slo.js';
import { patientCredentials } from './lib/session.js';
import { uuid } from './lib/api.js';
import { summarize } from './lib/summary.js';

const START = Number(__ENV.LOGIN_START_RATE || 2);
const MAX = Number(__ENV.LOGIN_MAX_RATE || 20);
const STEP = Number(__ENV.LOGIN_STEP || 2);
const STEP_DURATION_S = Number(__ENV.LOGIN_STEP_DURATION_S || 30);

const STAGE_MS = (5 + STEP_DURATION_S) * 1000;
const stages = [];
const stageRates = [];
for (let rate = START; rate <= MAX; rate += STEP) {
  stages.push({ target: rate, duration: '5s' }, { target: rate, duration: `${STEP_DURATION_S}s` });
  stageRates.push(rate);
}
// Per-stage sub-metrics so the summary shows accepted vs shed counts at each offered rate.
const perStage = {};
for (const rate of stageRates) {
  perStage[`qos_login_accepted{stage_rate:${rate}}`] = ['count>=0'];
  perStage[`qos_login_shed{stage_rate:${rate}}`] = ['count>=0'];
  perStage[`slo_login_accepted_duration{stage_rate:${rate}}`] = ['p(95)>=0'];
}

const accepted = new Trend('slo_login_accepted_duration', true);
const shedDuration = new Trend('qos_login_shed_duration', true);
const acceptedCount = new Counter('qos_login_accepted');
const shedCount = new Counter('qos_login_shed');
const throttledCount = new Counter('qos_login_throttled');
const timeouts = new Counter('qos_login_timeouts');
const otherErrors = new Rate('qos_login_unexpected_error');
const shedWithRetryAfter = new Rate('qos_login_shed_has_retry_after');

export const options = {
  scenarios: {
    logins: {
      executor: 'ramping-arrival-rate', startRate: START, timeUnit: '1s', stages,
      // A login takes at most the 10 s gateway deadline; size the VU pool for that worst case.
      preAllocatedVUs: Math.ceil(MAX * 3), maxVUs: Math.ceil(MAX * 11),
    },
  },
  thresholds: {
    slo_login_accepted_duration: [`p(95)<${SLO.login.p95}`],
    qos_login_shed_duration: ['p(95)<300'],
    qos_login_timeouts: ['count==0'],
    qos_login_unexpected_error: ['rate<0.01'],
    qos_login_shed_has_retry_after: ['rate==1'],
    ...perStage,
  },
};

export function setup() {
  const probe = login(patientCredentials(0));
  if (probe.status === 429 && !probe.headers['X-Clinic-Load-Shed']) {
    fail('Login is rate limited per IP before the test starts. Raise AUTH_RATE_LIMIT_PERMIT_LIMIT and IDENTITY_AUTH_RATE_LIMIT_PERMIT_LIMIT for this test (see header comment).');
  }
  if (probe.status !== 200) fail(`Probe login failed with HTTP ${probe.status}; check credentials / LOAD_USER_POOL.`);
  return { startedAt: Date.now() };
}

function stageRate(startedAt) {
  const index = Math.min(stageRates.length - 1, Math.floor((Date.now() - startedAt) / STAGE_MS));
  return stageRates[Math.max(0, index)];
}

function login(credentials) {
  return http.post(`${BASE_URL}/auth/login`, JSON.stringify(credentials), {
    headers: { ...DEFAULT_HEADERS, 'X-Correlation-ID': uuid() },
    tags: { endpoint: 'auth-login', cls: 'login' },
    timeout: '15s',
    responseCallback: http.expectedStatuses(200, 429, 503),
  });
}

export default function (data) {
  const res = login(patientCredentials(__ITER + __VU * 100000));
  const ms = res.timings.duration;
  const tags = { stage_rate: String(stageRate(data.startedAt)) };
  const shed = res.status === 503 && !!res.headers['X-Clinic-Load-Shed'];
  if (res.status === 200) {
    accepted.add(ms, tags);
    acceptedCount.add(1, tags);
  } else if (shed) {
    shedDuration.add(ms, tags);
    shedCount.add(1, tags);
    shedWithRetryAfter.add(Number(res.headers['Retry-After']) > 0);
  } else if (res.status === 429) {
    throttledCount.add(1);
  } else if (res.status === 504 || res.status === 0) {
    timeouts.add(1);
  }
  otherErrors.add(!(res.status === 200 || res.status === 429 || shed));
  check(res, { 'login accepted or shed (no timeout)': () => res.status === 200 || res.status === 429 || shed });
}

export function handleSummary(data) {
  const m = data.metrics;
  const durationS = (data.state?.testRunDurationMs || 1) / 1000;
  return summarize('login-capacity', data, {
    startRate: START,
    maxRate: MAX,
    stepRate: STEP,
    stepDurationS: STEP_DURATION_S,
    acceptedLoginsPerSecondAvg: Number(((m.qos_login_accepted?.values?.count || 0) / durationS).toFixed(2)),
    note: `Capacity = highest stage_rate whose qos_login_shed count stays ~0 and whose accepted p95 <= ${SLO.login.p95} ms. Each stage lasts ${STEP_DURATION_S + 5}s.`,
  });
}
