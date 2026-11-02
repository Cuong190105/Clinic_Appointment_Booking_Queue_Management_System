import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS, SLO_THRESHOLDS } from './config.js';

export const options = {
  stages: [
    { duration: '30s', target: 15 },  // Ramp-up to 15 VUs
    { duration: '1m', target: 30 },   // Normal load at 30 VUs
    { duration: '30s', target: 50 },  // Peak load at 50 VUs
    { duration: '30s', target: 0 },   // Ramp-down
  ],
  thresholds: SLO_THRESHOLDS,
};

export default function () {
  // 1. Gateway API Catalog discovery
  const catalogRes = http.get(`${BASE_URL}/api/catalog`, {
    headers: { ...DEFAULT_HEADERS, 'X-Correlation-ID': `load-${__VU}-${__ITER}` },
  });
  check(catalogRes, {
    'catalog returned 200': (r) => r.status === 200,
  });

  // 2. Doctor / Specialty browsing
  const specialRes = http.get(`${BASE_URL}/specialties`, {
    headers: { ...DEFAULT_HEADERS, 'X-Correlation-ID': `load-${__VU}-${__ITER}` },
  });
  check(specialRes, {
    'specialties returned valid response': (r) => r.status === 200 || r.status === 401,
  });

  // 3. Queue public status
  const queueRes = http.get(`${BASE_URL}/queue/status`, {
    headers: { ...DEFAULT_HEADERS, 'X-Correlation-ID': `load-${__VU}-${__ITER}` },
  });
  check(queueRes, {
    'queue status responded within SLA': (r) => r.timings.duration < 500,
  });

  sleep(0.5);
}
