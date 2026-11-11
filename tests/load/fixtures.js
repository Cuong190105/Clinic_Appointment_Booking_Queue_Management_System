import http from 'k6/http';
import {
  ADMIN_PASSWORD,
  ADMIN_USERNAME,
  BASE_URL,
  DEFAULT_HEADERS,
  DOCTOR_ID,
  responseItems,
  unwrapResponse,
} from './config.js';

export function login(username, password) {
  const response = http.post(
    `${BASE_URL}/auth/login`,
    JSON.stringify({ username, password }),
    { headers: DEFAULT_HEADERS, tags: { operation: 'auth-login' } }
  );

  if (response.status !== 200) {
    throw new Error(`Login failed for ${username}: HTTP ${response.status}`);
  }

  const body = unwrapResponse(JSON.parse(response.body));
  const token = body?.accessToken || body?.token;
  if (!token) throw new Error(`Login response did not contain an access token for ${username}`);
  return token;
}

export function adminToken() {
  return login(ADMIN_USERNAME, ADMIN_PASSWORD);
}

export function ensureWorkSchedule(token, date, startTime, endTime, recommendedCapacity = 100) {
  const createResponse = http.post(
    `${BASE_URL}/doctors/${DOCTOR_ID}/shifts`,
    JSON.stringify({ date, startTime, endTime, recommendedCapacity }),
    {
      headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${token}` },
      tags: { operation: 'benchmark-fixture-create-shift' },
    }
  );

  if (createResponse.status === 200 || createResponse.status === 201) {
    const schedule = unwrapResponse(JSON.parse(createResponse.body));
    if (!schedule?.id) throw new Error('Create shift response did not include a schedule ID');
    return {
      id: schedule.id,
      date,
      startTime: schedule.startTime || schedule.shiftStart || startTime,
      endTime: schedule.endTime || schedule.shiftEnd || endTime,
      createdByTest: true,
    };
  }

  // A schedule may already exist for this doctor/date. Reuse only an active schedule.
  const listResponse = http.get(
    `${BASE_URL}/doctors/${DOCTOR_ID}/shifts?startDate=${date}&endDate=${date}`,
    { headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${token}` }, tags: { operation: 'benchmark-fixture-find-shift' } }
  );
  if (listResponse.status === 200) {
    const schedules = responseItems(JSON.parse(listResponse.body));
    const existing = schedules.find((item) => item.status === 'Active' || item.status === 0);
    if (existing?.id) {
      return {
        id: existing.id,
        date,
        startTime: existing.startTime || existing.shiftStart,
        endTime: existing.endTime || existing.shiftEnd,
        createdByTest: false,
      };
    }
  }

  throw new Error(
    `Could not create a benchmark shift or find an active one for doctor ${DOCTOR_ID} on ${date}; create HTTP ${createResponse.status}, list HTTP ${listResponse.status}`
  );
}

export function availableDoctorSchedules(date) {
  const response = http.get(
    `${BASE_URL}/doctors/${DOCTOR_ID}/available?date=${date}`,
    { headers: DEFAULT_HEADERS, tags: { operation: 'schedule-availability' } }
  );
  if (response.status !== 200) {
    throw new Error(`Doctor availability query failed: HTTP ${response.status}`);
  }
  return responseItems(JSON.parse(response.body), ['timeSlot', 'timeSlots']);
}

export function slotsFromAvailability(schedules, scheduleId) {
  const schedule = schedules.find((item) => item.id === scheduleId);
  return Array.isArray(schedule?.timeSlot) ? schedule.timeSlot : Array.isArray(schedule?.timeSlots) ? schedule.timeSlots : [];
}

export function cancelWorkSchedule(token, schedule) {
  if (!schedule?.createdByTest || !schedule?.id) return true;
  const response = http.post(
    `${BASE_URL}/shifts/${schedule.id}/cancel`,
    JSON.stringify({ reason: 'Automated load-test fixture cleanup' }),
    { headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${token}` }, tags: { operation: 'benchmark-fixture-cleanup' } }
  );
  return response.status === 200 || response.status === 204;
}

export function parseAppointment(body) {
  return unwrapResponse(body);
}
