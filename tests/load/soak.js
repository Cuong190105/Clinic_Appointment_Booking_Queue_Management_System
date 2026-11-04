import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS, login, getAuthHeaders, getCachedTokens } from './config.js';

http.setResponseCallback(http.expectedStatuses(200, 204, 429));

export const options = {
  stages: [
    { duration: '10s', target: 15 },
    { duration: '40s', target: 15 }, // Steady endurance
    { duration: '10s', target: 0 },
  ],
  thresholds: {
    // 5xx errors must be < 1%
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500', 'p(99)<1500'],
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
  const vuIndex = (__VU - 1) % data.tokens.length;
  const token = data.tokens[vuIndex];
  const authHeaders = token ? getAuthHeaders(token) : DEFAULT_HEADERS;

  // Rotate between Read browsing and Authenticated status
  if (__ITER % 3 === 0) {
    const res = http.get(`${BASE_URL}/specialties`, {
      headers: { ...authHeaders, 'X-Correlation-ID': `soak-spec-${__VU}-${__ITER}` },
    });
    check(res, { 'specialties handled (200/429)': (r) => r.status === 200 || r.status === 429 });
  } else if (__ITER % 3 === 1) {
    const res = http.get(`${BASE_URL}/doctors`, {
      headers: { ...authHeaders, 'X-Correlation-ID': `soak-doc-${__VU}-${__ITER}` },
    });
    check(res, { 'doctors handled (200/429)': (r) => r.status === 200 || r.status === 429 });
  } else {
    if (token) {
      const res = http.get(`${BASE_URL}/me/appointments`, {
        headers: getAuthHeaders(token, null, `soak-me-${__VU}-${__ITER}`),
      });
      check(res, { 'me/appointments handled (200/429)': (r) => r.status === 200 || r.status === 429 });
    }
  }

  sleep(1.2);
}
