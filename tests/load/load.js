import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS, SLO_THRESHOLDS, clinicDate, responseItems, unwrapResponse } from './config.js';

const slotsUrl = `${BASE_URL}/slots?fromDate=${clinicDate(1)}T00:00:00&toDate=${clinicDate(7)}T23:59:59`;

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
  // Read mix: specialties discovery and availability within a bounded future week.
  const specRes = http.get(`${BASE_URL}/specialties`, {
    headers: { ...DEFAULT_HEADERS, 'X-Correlation-ID': `load-${__VU}-${__ITER}` },
  });
  check(specRes, {
    'specialties returned 200': (r) => r.status === 200,
    'specialties contains items': (r) => {
      try {
        const body = unwrapResponse(JSON.parse(r.body));
        return (Array.isArray(body) ? body : body?.items)?.length > 0;
      } catch {
        return false;
      }
    },
  });

  const slotsRes = http.get(slotsUrl, {
    headers: { ...DEFAULT_HEADERS, 'X-Correlation-ID': `load-${__VU}-${__ITER}` },
  });
  check(slotsRes, {
    'availability returned 200 with schedules': (r) => {
      if (r.status !== 200) return false;
      try {
        return responseItems(JSON.parse(r.body)).length > 0;
      } catch {
        return false;
      }
    },
  });

  sleep(0.5);
}
