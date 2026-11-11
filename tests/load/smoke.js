import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS, clinicDate, responseItems, unwrapResponse } from './config.js';

const slotsUrl = `${BASE_URL}/slots?fromDate=${clinicDate(1)}T00:00:00&toDate=${clinicDate(7)}T23:59:59`;

export const options = {
  vus: 2,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<300'],
  },
};

export default function () {
  // Smoke tests assert both status and a usable response shape.
  const specRes = http.get(`${BASE_URL}/specialties`, { headers: DEFAULT_HEADERS });
  check(specRes, {
    'specialties status is 200': (r) => r.status === 200,
    'specialties contains items': (r) => {
      try {
        const body = unwrapResponse(JSON.parse(r.body));
        const items = Array.isArray(body) ? body : body?.items;
        return Array.isArray(items) && items.length > 0;
      } catch {
        return false;
      }
    },
  });

  // Query a bounded future window so the smoke request resembles booking discovery.
  const slotsRes = http.get(slotsUrl, { headers: DEFAULT_HEADERS });
  check(slotsRes, {
    'slots returned 200 with available schedules': (r) => {
      if (r.status !== 200) return false;
      try {
        return responseItems(JSON.parse(r.body)).length > 0;
      } catch {
        return false;
      }
    },
  });

  sleep(1);
}
