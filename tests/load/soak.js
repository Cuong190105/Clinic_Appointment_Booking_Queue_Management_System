import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS, clinicDate, responseItems, unwrapResponse } from './config.js';

const slotsUrl = `${BASE_URL}/slots?fromDate=${clinicDate(1)}T00:00:00&toDate=${clinicDate(7)}T23:59:59`;

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
  const readSpecialties = __ITER % 5 === 0;
  const url = readSpecialties ? `${BASE_URL}/specialties` : slotsUrl;
  const res = http.get(url, {
    headers: { ...DEFAULT_HEADERS, 'X-Correlation-ID': `soak-${__VU}-${__ITER}` },
  });

  check(res, {
    'read API responds consistently during soak': (r) => {
      if (r.status !== 200) return false;
      try {
        return readSpecialties
          ? ((body) => (Array.isArray(body) ? body : body?.items)?.length > 0)(unwrapResponse(JSON.parse(r.body)))
          : responseItems(JSON.parse(r.body)).length > 0;
      } catch {
        return false;
      }
    },
  });

  sleep(1);
}
