import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';
import { BASE_URL, DEFAULT_HEADERS, clinicDate, responseItems, unwrapResponse } from './config.js';

const serverErrorRate = new Rate('spike_server_error_rate');
const unexpectedStatusRate = new Rate('spike_unexpected_status_rate');
const rateLimitedRate = new Rate('spike_rate_limited_rate');
const slotsUrl = `${BASE_URL}/slots?fromDate=${clinicDate(1)}T00:00:00&toDate=${clinicDate(7)}T23:59:59`;

export const options = {
  stages: [
    { duration: '10s', target: 5 },   // Warm-up
    { duration: '10s', target: 100 }, // Sudden spike to 100 VUs
    { duration: '30s', target: 100 }, // Hold spike
    { duration: '10s', target: 5 },   // Fast scale-down
    { duration: '10s', target: 0 },   // Recovery
  ],
  thresholds: {
    // 429 is measured separately as an expected load-shedding response.
    spike_server_error_rate: ['rate<0.05'],
    spike_unexpected_status_rate: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
  },
};

export default function () {
  const readSpecialties = __ITER % 3 === 0;
  const url = readSpecialties ? `${BASE_URL}/specialties` : slotsUrl;
  const res = http.get(url, {
    headers: { ...DEFAULT_HEADERS, 'X-Correlation-ID': `spike-${__VU}-${__ITER}` },
  });

  const serverError = res.status >= 500;
  const rateLimited = res.status === 429;
  const expectedStatus = res.status === 200 || rateLimited;
  serverErrorRate.add(serverError);
  unexpectedStatusRate.add(!expectedStatus);
  rateLimitedRate.add(rateLimited);

  check(res, {
    'spike returns data or explicit rate limit': (r) => {
      if (r.status === 429) return true;
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

  sleep(0.1);
}
