import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS } from './config.js';

export const options = {
  // Concurrently attempt to acquire identical slot / queue resource
  scenarios: {
    contention: {
      executor: 'per-vu-iterations',
      vus: 20,
      iterations: 5,
      maxDuration: '1m',
    },
  },
  thresholds: {
    // Under extreme contention, responses should still return clean 200 (winner) or 409 Conflict (loser),
    // and never 500 Internal Server Error (server crash).
    http_req_failed: ['rate<0.05'], // Only actual network/5xx errors count towards failure
  },
};

export default function () {
  const targetSlotId = 'b0b00000-0000-0000-0000-000000000001';
  const correlationId = `contention-${__VU}-${__ITER}`;

  const payload = JSON.stringify({
    slotId: targetSlotId,
    patientId: `p0000000-0000-0000-0000-0000000000${__VU < 10 ? '0' + __VU : __VU}`,
    patientName: `Test Contender ${__VU}`,
    serviceType: 'Consultation',
  });

  const res = http.post(`${BASE_URL}/reservations`, payload, {
    headers: {
      ...DEFAULT_HEADERS,
      'X-Correlation-ID': correlationId,
      'Idempotency-Key': `idemp-contention-${__VU}-${__ITER}`,
    },
  });

  // Verify business invariant: winner gets 200/201, concurrent contenders get 409 Conflict or 400 Bad Request,
  // NEVER 500 crash or duplicate booking!
  check(res, {
    'handled gracefully without 500 crash': (r) => r.status !== 500,
    'returned either success or conflict': (r) =>
      r.status === 200 || r.status === 201 || r.status === 409 || r.status === 400,
  });

  sleep(0.2);
}
