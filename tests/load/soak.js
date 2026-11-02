import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS } from './config.js';

export const options = {
  // Extended endurance test: 20 VUs for 5 minutes
  stages: [
    { duration: '30s', target: 20 },
    { duration: '4m', target: 20 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<400', 'p(99)<1200'],
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/api/catalog`, {
    headers: { ...DEFAULT_HEADERS, 'X-Correlation-ID': `soak-${__VU}-${__ITER}` },
  });

  check(res, {
    'catalog responds consistently during soak': (r) => r.status === 200,
  });

  sleep(1);
}
