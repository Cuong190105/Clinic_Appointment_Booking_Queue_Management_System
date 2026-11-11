import http from 'k6/http';
import { check, Counter } from 'k6';
import { BASE_URL, DEFAULT_HEADERS, DOCTOR_ID, PATIENT_PASSWORD, PATIENT_USERNAME, clinicDate } from './config.js';
import {
  adminToken as getAdminToken,
  availableDoctorSchedules,
  cancelWorkSchedule,
  ensureWorkSchedule,
  login,
  slotsFromAvailability,
} from './fixtures.js';

const winnersCounter = new Counter('contention_winners');
const conflictsCounter = new Counter('contention_conflicts');
const serverErrors = new Counter('contention_server_errors');
const testVus = Number(__ENV.CONTENTION_VUS || 10);

export const options = {
  scenarios: {
    one_hot_slot: {
      executor: 'per-vu-iterations',
      vus: testVus,
      iterations: 1,
      maxDuration: '30s',
    },
  },
  thresholds: {
    contention_winners: ['count==1'],
    contention_conflicts: [`count==${testVus - 1}`],
    contention_server_errors: ['count==0'],
  },
};

function timeStringToMinutes(value) {
  const [hour, minute] = String(value).split(':').map(Number);
  return hour * 60 + minute;
}

function formatTime(minutes) {
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}:00`;
}

export function setup() {
  if (!Number.isInteger(testVus) || testVus < 2) throw new Error('CONTENTION_VUS must be an integer >= 2.');

  const admin = getAdminToken();
  const patient = login(PATIENT_USERNAME, PATIENT_PASSWORD);
  const date = clinicDate(0);
  const clinicNow = new Date(Date.now() + Number(__ENV.CLINIC_UTC_OFFSET_MINUTES || 420) * 60_000);
  const nowMinutes = clinicNow.getUTCHours() * 60 + clinicNow.getUTCMinutes();
  const startMinutes = Math.ceil((nowMinutes + 45) / 60) * 60;
  if (startMinutes >= 23 * 60) {
    throw new Error('No reliable same-day time remains for check-in contention. Run during clinic hours.');
  }

  const schedule = ensureWorkSchedule(admin, date, formatTime(startMinutes), '23:59:00', 100);
  const available = availableDoctorSchedules(date);
  const slots = slotsFromAvailability(available, schedule.id);
  const targetSlot = slots.find((slot) => timeStringToMinutes(slot) >= nowMinutes + 30);
  if (!targetSlot) {
    if (schedule.createdByTest) cancelWorkSchedule(admin, schedule);
    throw new Error(`No free future appointment slot found for doctor ${DOCTOR_ID} on ${date}.`);
  }

  return {
    adminToken: admin,
    patientToken: patient,
    schedule,
    date,
    targetSlot,
    reasonPrefix: `Contention load test ${Date.now()}`,
    runId: `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`,
  };
}

export default function (data) {
  const headers = {
    ...DEFAULT_HEADERS,
    Authorization: `Bearer ${data.patientToken}`,
    'X-Correlation-ID': `contention-${data.runId}-${__VU}`,
    'Idempotency-Key': `contention-${data.runId}-${__VU}`,
  };
  const payload = JSON.stringify({
    workScheduleId: data.schedule.id,
    timeSlot: data.targetSlot,
    reason: `${data.reasonPrefix} VU ${__VU}`,
  });

  const response = http.post(`${BASE_URL}/appointments`, payload, { headers, tags: { operation: 'appointment-hot-slot' } });
  if (response.status === 200 || response.status === 201) winnersCounter.add(1);
  else if (response.status === 400 || response.status === 409) conflictsCounter.add(1);
  else if (response.status >= 500) serverErrors.add(1);

  check(response, {
    'booking was created or rejected as occupied slot': (r) =>
      r.status === 200 || r.status === 201 || r.status === 400 || r.status === 409,
  });
}

export function teardown(data) {
  if (!data?.patientToken || !data?.adminToken) return;

  const listResponse = http.get(`${BASE_URL}/me/appointments`, {
    headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${data.patientToken}` },
    tags: { operation: 'contention-invariant-read' },
  });
  if (listResponse.status !== 200) {
    throw new Error(`Could not verify hot-slot bookings: GET /me/appointments returned ${listResponse.status}`);
  }

  const body = JSON.parse(listResponse.body);
  const appointments = Array.isArray(body) ? body : body.result || body.data || [];
  const testAppointments = appointments.filter((appointment) =>
    appointment.reason?.startsWith(data.reasonPrefix) && appointment.timeSlot?.startsWith(data.targetSlot.slice(0, 5))
  );
  let cleanupFailed = false;

  for (const appointment of testAppointments) {
    const canceled = http.post(
      `${BASE_URL}/appointments/${appointment.id}/cancel`,
      JSON.stringify({ reason: 'Automated contention test cleanup' }),
      { headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${data.adminToken}` }, tags: { operation: 'contention-cleanup' } }
    );
    let verified = false;
    if (canceled.status === 200 || canceled.status === 204) {
      for (let attempt = 0; attempt < 10; attempt++) {
        const detail = http.get(`${BASE_URL}/appointments/${appointment.id}`, {
          headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${data.adminToken}` },
          tags: { operation: 'contention-cleanup-verify' },
        });
        if (detail.status === 200) {
          const parsed = JSON.parse(detail.body);
          const item = parsed.result || parsed.data || parsed;
          if (item.status === 'Cancelled') {
            verified = true;
            break;
          }
        }
      }
    }
    if (!verified) cleanupFailed = true;
  }

  const invariantPassed = testAppointments.length === 1;
  check(invariantPassed, { 'database has exactly one appointment for the contested slot': (value) => value });
  check(!cleanupFailed, { 'all test appointments are cancelled and verified': (value) => value });

  const shiftCleanupPassed = cancelWorkSchedule(data.adminToken, data.schedule);
  if (!invariantPassed) {
    throw new Error(`Expected exactly one booking for ${data.targetSlot} on ${data.date}; found ${testAppointments.length}.`);
  }
  if (cleanupFailed || !shiftCleanupPassed) throw new Error('Contention test cleanup could not be verified.');
}
