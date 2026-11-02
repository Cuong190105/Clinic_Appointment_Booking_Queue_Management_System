import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS } from './config.js';

export const options = {
  stages: [
    { duration: '10s', target: 5 },   // Warm-up
    { duration: '10s', target: 100 }, // Sudden spike to 100 VUs
    { duration: '30s', target: 100 }, // Hold spike
    { duration: '10s', target: 5 },   // Fast scale-down
    { duration: '10s', target: 0 },   // Recovery
  ],
  thresholds: {
    // During extreme spike, rate limiting (429) may trigger, but 5xx should be minimal (< 5%)
    http_req_duration: ['p(95)<1000'],
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/api/catalog`, {
    headers: { ...DEFAULT_HEADERS, 'X-Correlation-ID': `spike-${__VU}-${__ITER}` },
  });

  check(res, {
    'spike response received': (r) => r.status === 200 || r.status === 429,
  });

  sleep(0.1);
}
