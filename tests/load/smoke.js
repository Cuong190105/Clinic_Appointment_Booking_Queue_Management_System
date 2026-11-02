import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS } from './config.js';

export const options = {
  vus: 2,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<300'],
  },
};

export default function () {
  // 1. Health / Catalog Check
  const catalogRes = http.get(`${BASE_URL}/api/catalog`, { headers: DEFAULT_HEADERS });
  check(catalogRes, {
    'catalog status is 200': (r) => r.status === 200,
    'catalog has services': (r) => JSON.parse(r.body).services?.length > 0,
  });

  // 2. Query available doctor slots
  const slotsRes = http.get(`${BASE_URL}/slots?date=2026-11-03`, { headers: DEFAULT_HEADERS });
  check(slotsRes, {
    'slots endpoint responded': (r) => r.status === 200 || r.status === 404,
  });

  sleep(1);
}
