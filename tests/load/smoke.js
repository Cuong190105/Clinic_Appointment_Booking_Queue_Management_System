import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, DEFAULT_HEADERS, login, getAuthHeaders } from './config.js';

http.setResponseCallback(http.expectedStatuses(200, 201, 204, 409));

export const options = {
  vus: 1,
  duration: '10s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
  },
};

export function setup() {
  const patientToken = login('patient1');
  const receptionistToken = login('receptionist1');
  return { patientToken, receptionistToken };
}

export default function (data) {
  const { patientToken, receptionistToken } = data;

  // 1. Gateway Catalog Discovery
  const catalogRes = http.get(`${BASE_URL}/api/catalog`, { headers: DEFAULT_HEADERS });
  check(catalogRes, {
    'catalog returns 200': (r) => r.status === 200,
    'catalog has microservices': (r) => JSON.parse(r.body).result?.services?.length >= 5,
  });

  // 2. Specialties Browse
  const specRes = http.get(`${BASE_URL}/specialties`, { headers: DEFAULT_HEADERS });
  check(specRes, {
    'specialties returns 200': (r) => r.status === 200,
  });

  // 3. Doctors Browse
  const docRes = http.get(`${BASE_URL}/doctors`, { headers: DEFAULT_HEADERS });
  check(docRes, {
    'doctors returns 200': (r) => r.status === 200,
  });

  // 4. Authenticated Patient: My Appointments
  if (patientToken) {
    const myAppRes = http.get(`${BASE_URL}/me/appointments`, {
      headers: getAuthHeaders(patientToken, null, 'smoke-patient-apps'),
    });
    check(myAppRes, {
      'me/appointments returns 200': (r) => r.status === 200,
    });

    // 5. Authenticated Patient: Queue Status
    const queueRes = http.get(`${BASE_URL}/me/queue-status`, {
      headers: getAuthHeaders(patientToken, null, 'smoke-queue-status'),
    });
    check(queueRes, {
      'me/queue-status returns 200 or 204': (r) => r.status === 200 || r.status === 204,
    });

    // 6. Authenticated Patient: In-app Notifications
    const notifRes = http.get(`${BASE_URL}/notifications`, {
      headers: getAuthHeaders(patientToken, null, 'smoke-notifs'),
    });
    check(notifRes, {
      'notifications returns 200': (r) => r.status === 200,
    });
  }

  // 7. Authenticated Receptionist Check-in
  if (receptionistToken) {
    const rawIds = __ENV.CHECKIN_APP_IDS || '';
    const appIds = rawIds ? rawIds.split(',') : [];
    if (appIds.length > 0) {
      const targetAppId = appIds[__ITER % appIds.length];
      const checkinRes = http.post(
        `${BASE_URL}/appointments/${targetAppId}/check-in`,
        null,
        {
          headers: getAuthHeaders(
            receptionistToken,
            `smoke-checkin-${targetAppId}`,
            'smoke-checkin-test'
          ),
        }
      );
      check(checkinRes, {
        'check-in handles successfully or conflict cleanly': (r) =>
          r.status === 204 || r.status === 200 || r.status === 409,
      });
    }
  }

  sleep(1);
}

