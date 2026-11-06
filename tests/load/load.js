import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS, SLO_THRESHOLDS, login, getAuthHeaders, getCachedTokens } from './config.js';

http.setResponseCallback(http.expectedStatuses(200, 201, 204, 409, 429));

export const options = {
  stages: [
    { duration: '10s', target: 5 },  // Ramp-up
    { duration: '20s', target: 15 }, // Sustained load
    { duration: '10s', target: 15 }, // Peak load
    { duration: '10s', target: 0 },  // Ramp-down
  ],
  thresholds: SLO_THRESHOLDS,
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
  const authHeaders = token ? getAuthHeaders(token) : DEFAULT_HEADERS;

  // Realistic Weighted Workload Mix:
  // 35% Specialties Discovery, 30% Doctors Discovery, 20% Personal History, 15% Booking
  const roll = Math.random();

  if (roll < 0.35) {
    // 1. Catalog: Specialties
    const res = http.get(`${BASE_URL}/specialties`, {
      headers: { ...authHeaders, 'X-Correlation-ID': `load-spec-${__VU}-${__ITER}` },
    });
    check(res, { 'specialties handled (200/429)': (r) => r.status === 200 || r.status === 429 });
  } else if (roll < 0.65) {
    // 2. Catalog: Doctors
    const res = http.get(`${BASE_URL}/doctors`, {
      headers: { ...authHeaders, 'X-Correlation-ID': `load-doc-${__VU}-${__ITER}` },
    });
    check(res, { 'doctors handled (200/429)': (r) => r.status === 200 || r.status === 429 });
  } else if (roll < 0.85) {
    // 3. Personal Appointment History
    if (token) {
      const res = http.get(`${BASE_URL}/me/appointments`, {
        headers: getAuthHeaders(token, null, `load-me-${__VU}-${__ITER}`),
      });
      check(res, { 'me/appointments handled (200/429)': (r) => r.status === 200 || r.status === 429 });
    }
  } else {
    // 4. Appointment Booking (Write Workload)
    if (token) {
      const workloadScheduleId = __ENV.WORKLOAD_SCHEDULE_ID;
      if (!workloadScheduleId) {
        throw new Error('Missing mandatory __ENV.WORKLOAD_SCHEDULE_ID for load test');
      }
      const slotIdx = (__ITER + __VU) % 48;
      const hour = 8 + Math.floor(slotIdx / 4);
      const minute = (slotIdx % 4) * 15;
      const timeSlotStr = `${hour < 10 ? '0' + hour : hour}:${minute < 10 ? '0' + minute : minute}:00`;
      const payload = JSON.stringify({
        workScheduleId: workloadScheduleId,
        timeSlot: timeSlotStr,
        reason: `[${testRunId}] Load test booking VU ${__VU} ITER ${__ITER}`,
        isWalkIn: false,
      });

      const bookRes = http.post(`${BASE_URL}/appointments`, payload, {
        headers: getAuthHeaders(
          token,
          `${testRunId}-load-vu${__VU}-it${__ITER}-${Date.now()}`,
          `corr-load-${__VU}-${__ITER}`
        ),
      });

      check(bookRes, {
        'booking handled cleanly (201/200/409)': (r) =>
          r.status === 201 || r.status === 200 || r.status === 409,
        'booking not crashed with 500': (r) => r.status < 500,
      });
    }
  }

  sleep(1.0);
}

