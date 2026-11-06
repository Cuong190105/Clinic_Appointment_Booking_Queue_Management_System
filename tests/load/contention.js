import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import { BASE_URL, DEFAULT_HEADERS, login, getAuthHeaders, getCachedTokens } from './config.js';

const winnerCounter = new Counter('contention_winners');
const conflictCounter = new Counter('contention_conflicts');
const crashCounter = new Counter('contention_5xx_crashes');

http.setResponseCallback(http.expectedStatuses(200, 201, 409));

export const options = {
  scenarios: {
    contention: {
      executor: 'per-vu-iterations',
      vus: 15,
      iterations: 1,
      maxDuration: '20s',
    },
  },
  thresholds: {
    contention_winners: ['count>=1'], // At least 1 winner
    contention_5xx_crashes: ['count==0'], // Zero crashes
  },
};

export function setup() {
  const cached = getCachedTokens();
  if (cached && cached.patients && cached.patients.length >= 15) {
    return { tokens: cached.patients.slice(0, 15).map((p) => p.token) };
  }
  const tokens = [];
  for (let i = 1; i <= 15; i++) {
    tokens.push(login(`patient${i}`));
  }
  return { tokens };
}

export default function (data) {
  const testRunId = __ENV.TEST_RUN_ID || 'PH9-TEST';
  const vuIndex = (__VU - 1) % data.tokens.length;
  const token = data.tokens[vuIndex];

  const targetScheduleId = __ENV.CONTENTION_SCHEDULE_ID;
  if (!targetScheduleId) {
    throw new Error('Missing mandatory __ENV.CONTENTION_SCHEDULE_ID for contention test');
  }
  const idempotencyKey = `${testRunId}-contention-vu${__VU}-iter${__ITER}-${Date.now()}`;
  const correlationId = `corr-contention-${__VU}-${__ITER}`;

  const payload = JSON.stringify({
    workScheduleId: targetScheduleId,
    timeSlot: '15:00:00',
    reason: `[${testRunId}] Contention stress test VU ${__VU}`,
    isWalkIn: false,
  });

  const res = http.post(`${BASE_URL}/appointments`, payload, {
    headers: getAuthHeaders(token, idempotencyKey, correlationId),
  });

  if (res.status === 201 || res.status === 200) {
    winnerCounter.add(1);
  } else if (res.status === 409) {
    conflictCounter.add(1);
  } else if (res.status >= 500) {
    crashCounter.add(1);
  }

  check(res, {
    'handled gracefully without 500 crash': (r) => r.status < 500,
    'returned valid winner (201) or conflict (409) status': (r) =>
      r.status === 201 || r.status === 200 || r.status === 409,
  });

  sleep(0.1);
}

