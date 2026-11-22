import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import {
  BASE_URL,
  DEFAULT_HEADERS,
  ADMIN_PASSWORD,
  ADMIN_USERNAME,
  DOCTOR_ID,
  PATIENT_PASSWORD,
  PATIENT_USERNAME,
  RECEPTIONIST_PASSWORD,
  RECEPTIONIST_USERNAME,
  clinicDate,
  responseItems,
  unwrapResponse,
} from './config.js';
import { cancelWorkSchedule, login } from './fixtures.js';

const PROFILE = String(__ENV.MIXED_PROFILE || 'normal').toLowerCase();
const VUS = Number(__ENV.MIXED_VUS || (PROFILE === 'clinic-peak' ? 30 : 20));
const DURATION = __ENV.MIXED_DURATION || '5m';
const RAMP_DURATION = __ENV.MIXED_RAMP || '30s';
const CLINIC_OFFSET = Number(__ENV.CLINIC_UTC_OFFSET_MINUTES || 420);
const IS_PEAK = PROFILE === 'clinic-peak';

if (!['normal', 'clinic-peak'].includes(PROFILE)) {
  throw new Error('MIXED_PROFILE must be normal or clinic-peak.');
}

const readRequests = new Counter('mixed_read_requests');
const writeRequests = new Counter('mixed_write_requests');
const readSuccessRate = new Rate('mixed_read_success_rate');
const writeSuccessRate = new Rate('mixed_write_success_rate');
const workflowSuccessRate = new Rate('mixed_workflow_success_rate');
const serverErrorRate = new Rate('mixed_server_error_rate');
const readDuration = new Trend('mixed_read_api_duration', true);
const writeDuration = new Trend('mixed_write_api_duration', true);
const bookingCreateDuration = new Trend('mixed_booking_create_duration', true);
const queueWorkflowDuration = new Trend('mixed_queue_workflow_duration', true);
const selectedReadJourneys = new Counter('mixed_read_journeys');
const selectedBookingJourneys = new Counter('mixed_booking_journeys');
const selectedQueueJourneys = new Counter('mixed_queue_journeys');
const bookingConfirmed = new Counter('mixed_booking_confirmed');
const checkinsCompleted = new Counter('mixed_checkins_completed');
const queueLifecycleCompleted = new Counter('mixed_queue_lifecycle_completed');

const requestThresholds = {
  mixed_read_api_duration: ['p(95)<1000'],
  mixed_write_api_duration: ['p(95)<1500'],
  mixed_booking_create_duration: ['p(95)<1500'],
  mixed_queue_workflow_duration: ['p(95)<10000'],
  mixed_read_success_rate: ['rate>0.98'],
  mixed_write_success_rate: ['rate>0.95'],
  mixed_workflow_success_rate: ['rate>0.90'],
  mixed_server_error_rate: ['rate<0.01'],
};

export const options = {
  stages: [
    { duration: RAMP_DURATION, target: VUS },
    { duration: DURATION, target: VUS },
    { duration: '30s', target: 0 },
  ],
  thresholds: requestThresholds,
};

function auth(token) {
  return { ...DEFAULT_HEADERS, Authorization: `Bearer ${token}` };
}

function resultOf(response) {
  try {
    return unwrapResponse(JSON.parse(response.body));
  } catch {
    return null;
  }
}

function statusOk(response, allowed = [200]) {
  return allowed.includes(response.status);
}

function getRead(path, token, allowed = [200], operation = 'mixed-read') {
  const response = http.get(`${BASE_URL}${path}`, {
    headers: token ? auth(token) : DEFAULT_HEADERS,
    tags: { operation },
  });
  readRequests.add(1);
  readDuration.add(response.timings.duration);
  readSuccessRate.add(statusOk(response, allowed));
  serverErrorRate.add(response.status >= 500);
  check(response, { [`${operation} returned expected status`]: (r) => allowed.includes(r.status) });
  return response;
}

function write(method, path, token, body = undefined, allowed = [200, 201, 204], operation = 'mixed-write', key = undefined) {
  const headers = auth(token);
  if (key) headers['Idempotency-Key'] = key;
  const params = { headers, tags: { operation } };
  let response;
  if (method === 'PATCH') response = http.patch(`${BASE_URL}${path}`, JSON.stringify(body ?? {}), params);
  else if (method === 'PUT') response = http.put(`${BASE_URL}${path}`, JSON.stringify(body ?? {}), params);
  else response = http.post(`${BASE_URL}${path}`, body === undefined ? null : JSON.stringify(body), params);
  writeRequests.add(1);
  writeDuration.add(response.timings.duration);
  writeSuccessRate.add(statusOk(response, allowed));
  serverErrorRate.add(response.status >= 500);
  check(response, { [`${operation} returned expected status`]: (r) => allowed.includes(r.status) });
  return response;
}

function appointmentId(response) {
  const value = resultOf(response);
  return value?.id || value?.appointmentId || null;
}

function queueTicket(response) {
  const value = resultOf(response);
  return value?.queueTicketId || value?.id || null;
}

function localMinutesNow() {
  const now = new Date(Date.now() + CLINIC_OFFSET * 60_000);
  return now.getUTCHours() * 60 + now.getUTCMinutes();
}

function timeMinutes(value) {
  const m = String(value || '').match(/(\d{2}):(\d{2})/);
  return m ? Number(m[1]) * 60 + Number(m[2]) : -1;
}

function timeString(minutes) {
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}:00`;
}

function queryAvailableSlots(doctorId, date, token = undefined) {
  const response = http.get(`${BASE_URL}/doctors/${doctorId}/available?date=${date}`, {
    headers: token ? auth(token) : DEFAULT_HEADERS,
    tags: { operation: 'mixed-fixture-availability' },
  });
  if (response.status !== 200) return [];
  const schedules = responseItems(JSON.parse(response.body), ['timeSlot', 'timeSlots']);
  const slots = [];
  for (const schedule of schedules) {
    const values = schedule.timeSlot || schedule.timeSlots || [];
    for (const slot of values) slots.push({ scheduleId: schedule.id, timeSlot: slot });
  }
  return slots;
}

function ensureSchedule(admin, doctorId, date, startTime, endTime) {
  const response = http.post(
    `${BASE_URL}/doctors/${doctorId}/shifts`,
    JSON.stringify({ date, startTime, endTime, recommendedCapacity: 100 }),
    { headers: auth(admin), tags: { operation: 'mixed-fixture-create-shift' } }
  );
  if (response.status === 200 || response.status === 201) {
    const schedule = resultOf(response);
    if (!schedule?.id) throw new Error(`Created schedule for ${date} without an ID.`);
    return { id: schedule.id, date, createdByTest: true };
  }

  const list = http.get(`${BASE_URL}/doctors/${doctorId}/shifts?startDate=${date}&endDate=${date}`, {
    headers: auth(admin), tags: { operation: 'mixed-fixture-find-shift' },
  });
  let active = null;
  if (list.status === 200) {
    const openSlots = queryAvailableSlots(doctorId, date);
    active = responseItems(JSON.parse(list.body)).find((schedule) =>
      (schedule.status === 'Active' || schedule.status === 0) && openSlots.some((slot) => slot.scheduleId === schedule.id)
    );
  }
  if (!active?.id) throw new Error(`Could not create or find active test shift for ${date} (HTTP ${response.status}/${list.status}).`);
  return { id: active.id, date, createdByTest: false };
}

function discoverSlots(doctorId, schedule, minimumMinutes = 0) {
  return queryAvailableSlots(doctorId, schedule.date)
    .filter((x) => x.scheduleId === schedule.id && timeMinutes(x.timeSlot) >= minimumMinutes)
    .map((x) => x.timeSlot)
    .sort((a, b) => timeMinutes(a) - timeMinutes(b));
}

export function setup() {
  const admin = login(ADMIN_USERNAME, ADMIN_PASSWORD);
  const patient = login(PATIENT_USERNAME, PATIENT_PASSWORD);
  const receptionist = login(RECEPTIONIST_USERNAME, RECEPTIONIST_PASSWORD);
  const patientProfileResponse = http.get(`${BASE_URL}/patients/me`, { headers: auth(patient) });
  if (patientProfileResponse.status !== 200) throw new Error(`Patient profile lookup failed: HTTP ${patientProfileResponse.status}.`);
  const patientProfile = resultOf(patientProfileResponse);
  if (!patientProfile?.id) throw new Error('Patient profile response did not contain a patient ID.');
  const doctorActors = [];
  const doctorCount = IS_PEAK ? 10 : 1;
  for (let i = 1; i <= doctorCount; i++) {
    const username = i === 1 ? (__ENV.BENCHMARK_DOCTOR_USERNAME || 'doctor1') : `doctor${i}`;
    const password = i === 1 ? (__ENV.BENCHMARK_DOCTOR_PASSWORD || 'Password123!') : 'Password123!';
    const doctorToken = login(username, password);
    const doctorResponse = http.get(`${BASE_URL}/doctors/me`, { headers: auth(doctorToken) });
    if (doctorResponse.status !== 200) throw new Error(`Doctor profile lookup failed for ${username}: HTTP ${doctorResponse.status}.`);
    const doctor = resultOf(doctorResponse);
    if (!doctor?.id) throw new Error(`Doctor profile response did not contain a doctor ID for ${username}.`);
    if (!doctor?.specialtyId) throw new Error(`Doctor profile response did not contain a specialty ID for ${username}.`);
    doctorActors.push({ doctorId: doctor.id, doctorToken, specialtyId: doctor.specialtyId });
  }
  const doctorId = doctorActors[0].doctorId;

  const futureDate = clinicDate(3);
  const futureSchedule = ensureSchedule(admin, doctorId, futureDate, '08:00:00', '20:00:00');
  const futureSlots = discoverSlots(doctorId, futureSchedule);
  if (futureSlots.length < VUS) {
    if (futureSchedule.createdByTest) cancelWorkSchedule(admin, futureSchedule);
    throw new Error(`Only ${futureSlots.length} future slots available for ${VUS} VUs; refusing to run a collision-prone fixture.`);
  }

  const todaySchedules = [];
  let todaySlots = [];
  if (IS_PEAK) {
    const nowMinutes = localMinutesNow();
    const firstSlot = Math.ceil((nowMinutes + 20) / 15) * 15;
    if (firstSlot >= 22 * 60) {
      if (futureSchedule.createdByTest) cancelWorkSchedule(admin, futureSchedule);
      throw new Error('Clinic-peak queue flow needs at least one future same-day slot before 22:00 local clinic time.');
    }
    const today = clinicDate(0);
    for (const actor of doctorActors) {
      const schedule = ensureSchedule(admin, actor.doctorId, today, timeString(firstSlot), '23:59:00');
      todaySchedules.push(schedule);
      const slots = discoverSlots(actor.doctorId, schedule, firstSlot);
      for (const slot of slots) todaySlots.push({ ...actor, scheduleId: schedule.id, slot });
    }
    if (todaySlots.length < VUS) {
      for (const schedule of todaySchedules) if (schedule.createdByTest) cancelWorkSchedule(admin, schedule);
      if (futureSchedule.createdByTest) cancelWorkSchedule(admin, futureSchedule);
      throw new Error(`Only ${todaySlots.length} same-day future slots available for ${VUS} VUs; refusing queue fixture collisions.`);
    }
  }

  const runId = `mixed-${PROFILE}-${Date.now()}`;
  console.log(`[MIXED SETUP] profile=${PROFILE} VUs=${VUS} futureSlots=${futureSlots.length} queueDoctorCount=${doctorActors.length} todayQueueSlots=${todaySlots.length}`);
  return { admin, patient, patientId: patientProfile.id, receptionist, doctorActors, doctorId, futureSchedule, futureSlots, todaySchedules, todaySlots, runId };
}

function readJourney(data) {
  selectedReadJourneys.add(1);
  const choices = [
    () => getRead('/specialties', null, [200], 'read-specialties'),
    () => getRead('/doctors', null, [200], 'read-doctors'),
    () => getRead(`/doctors/${data.doctorId}/available?date=${clinicDate(1)}`, null, [200], 'read-availability'),
    () => getRead('/me/appointments?category=upcoming', data.patient, [200], 'read-my-appointments'),
    () => getRead('/me/queue-status', data.patient, [200, 204], 'read-my-queue-status'),
    () => getRead('/receptionist/appointments/pending', data.receptionist, [200], 'read-reception-pending'),
    () => getRead('/receptionist/queue-doctors', data.receptionist, [200], 'read-reception-queue-doctors'),
    () => {
      const actor = data.doctorActors[(__VU - 1) % data.doctorActors.length];
      return getRead(`/doctors/${actor.doctorId}/queue?date=${clinicDate(0)}`, actor.doctorToken, [200], 'read-doctor-queue');
    },
    () => getRead('/me/medical-history', data.patient, [200], 'read-medical-history'),
  ];
  const response = choices[Math.floor(Math.random() * choices.length)]();
  const good = response.status === 200 || (response.url.includes('/me/queue-status') && response.status === 204);
  workflowSuccessRate.add(good);
  return good;
}

function bookingJourney(data) {
  selectedBookingJourneys.add(1);
  const slot = data.futureSlots[(__VU - 1) % data.futureSlots.length];
  const idempotencyKey = `${data.runId}-book-${__VU}-${__ITER}`;
  const payload = { workScheduleId: data.futureSchedule.id, timeSlot: slot, reason: `Mixed load ${data.runId}` };
  let id = null;
  let success = false;
  const createStart = Date.now();
  const create = write('POST', '/appointments', data.patient, payload, [200, 201], 'write-booking-create', idempotencyKey);
  bookingCreateDuration.add(create.timings.duration);
  id = appointmentId(create);
  if (!id) {
    workflowSuccessRate.add(false);
    return false;
  }

  try {
    let confirmed = false;
    const deadline = Date.now() + 8_000;
    while (Date.now() < deadline) {
      const detail = getRead(`/appointments/${id}`, data.patient, [200], 'read-booking-confirmation');
      if (detail.status === 200 && resultOf(detail)?.status === 'Confirmed') {
        confirmed = true;
        break;
      }
      sleep(0.08);
    }
    if (!confirmed) return false;
    bookingConfirmed.add(1);

    const retry = write('POST', '/appointments', data.patient, payload, [200, 201], 'write-booking-idempotency-retry', idempotencyKey);
    if (appointmentId(retry) !== id) return false;
    success = true;
    return true;
  } finally {
    const cancel = write('POST', `/appointments/${id}/cancel`, data.admin, undefined, [200, 204], 'write-booking-cleanup');
    let cancelled = false;
    for (let attempt = 0; attempt < 10; attempt++) {
      const detail = getRead(`/appointments/${id}`, data.admin, [200], 'read-booking-cleanup-verify');
      if (detail.status === 200 && resultOf(detail)?.status === 'Cancelled') {
        cancelled = true;
        break;
      }
      sleep(0.05);
    }
    check(cancel, { 'booking cleanup request accepted': (r) => r.status === 200 || r.status === 204 });
    check(cancelled, { 'booking cleanup verified as Cancelled': (v) => v === true });
    if (!cancelled) success = false;
    workflowSuccessRate.add(success);
    void createStart;
  }
}

function queueJourney(data) {
  selectedQueueJourneys.add(1);
  const started = Date.now();
  const slotFixture = data.todaySlots[(__VU - 1) % data.todaySlots.length];
  const { doctorId, doctorToken } = slotFixture;
  const key = `${data.runId}-queue-${__VU}-${__ITER}`;
  let appointment = null;
  let ticket = null;
  let success = false;

  // Receptionist walk-in is the actual same-day check-in workflow: it confirms the encounter
  // and emits AppointmentCheckedIn atomically, which then creates the Queue ticket.
  const payload = {
    patientId: data.patientId,
    specialtyId: slotFixture.specialtyId,
    doctorId,
    reason: `Mixed queue ${data.runId}`,
    isUrgent: false,
  };
  const create = write('POST', '/receptionist/appointments/walk-in', data.receptionist, payload, [200, 201], 'peak-reception-walk-in', key);
  appointment = appointmentId(create);
  if (!appointment) {
    workflowSuccessRate.add(false);
    return false;
  }
  if (resultOf(create)?.status !== 'CheckedIn') {
    workflowSuccessRate.add(false);
    return false;
  }
  checkinsCompleted.add(1);

  try {
    const ticketDeadline = Date.now() + 8_000;
    while (Date.now() < ticketDeadline) {
      const response = getRead(`/queue/appointment/${appointment}`, data.receptionist, [200, 404], 'peak-queue-ticket-poll');
      if (response.status === 200) { ticket = queueTicket(response); break; }
      sleep(0.08);
    }
    if (!ticket) return false;

    const priority = write('PATCH', `/queue/${ticket}/priority`, data.receptionist, { priority: true }, [200, 204], 'peak-reception-priority');
    if (!statusOk(priority, [200, 204])) return false;
    const call = write('POST', `/queue/${ticket}/call`, data.receptionist, undefined, [200], 'peak-reception-call');
    if (call.status !== 200) return false;
    const doctorQueue = getRead(`/doctors/${doctorId}/queue?date=${clinicDate(0)}`, doctorToken, [200], 'peak-doctor-queue-read');
    if (doctorQueue.status !== 200) return false;
    const skip = write('POST', `/queue/${ticket}/skip`, doctorToken, undefined, [200], 'peak-doctor-skip');
    if (skip.status !== 200) return false;
    const retry = write('POST', `/queue/${ticket}/retry`, data.receptionist, undefined, [200], 'peak-reception-retry');
    if (retry.status !== 200) return false;

    success = true;
    queueLifecycleCompleted.add(1);
    return true;
  } finally {
    // Walk-in tickets are cancelled via the AppointmentCancelled event after the queue operations.
    const cancel = write('POST', `/appointments/${appointment}/cancel`, data.receptionist, undefined, [200, 204], 'peak-appointment-cleanup');
    let cancelled = false;
    for (let attempt = 0; attempt < 20; attempt++) {
      const response = getRead(`/appointments/${appointment}`, data.receptionist, [200], 'peak-appointment-cleanup-verify');
      if (response.status === 200 && resultOf(response)?.status === 'Cancelled') { cancelled = true; break; }
      sleep(0.05);
    }
    check(cancel, { 'queue test appointment cancellation accepted': (r) => r.status === 200 || r.status === 204 });
    check(cancelled, { 'queue test appointment cleanup verified': (v) => v === true });
    let ticketCancelled = !ticket;
    if (ticket) {
      for (let attempt = 0; attempt < 20; attempt++) {
        const queueState = getRead(`/queue/${ticket}`, data.receptionist, [200], 'peak-queue-cleanup-verify');
        const status = String(resultOf(queueState)?.status || '').toLowerCase();
        if (status === 'cancelled' || status === 'canceled') { ticketCancelled = true; break; }
        sleep(0.05);
      }
    }
    check(ticketCancelled, { 'queue ticket cleanup verified as cancelled': (v) => v === true });
    queueWorkflowDuration.add(Date.now() - started);
    workflowSuccessRate.add(success && cancelled && ticketCancelled);
  }
}

export default function (data) {
  const choice = Math.random();
  let ok;
  if (!IS_PEAK) {
    // 82% single-read journeys + 18% booking journey (3 reads, 3 writes)
    // yields approximately 70/30 HTTP read/write mix.
    ok = choice < 0.82 ? readJourney(data) : bookingJourney(data);
  } else {
    // 75% single-read + 20% booking journey + 5% queue lifecycle
    // yields approximately 60/40 HTTP read/write mix.
    ok = choice < 0.75 ? readJourney(data) : choice < 0.95 ? bookingJourney(data) : queueJourney(data);
  }
  if (!ok) check(ok, { 'selected mixed business journey completed': (value) => value === true });
  sleep(0.2 + Math.random() * 0.5);
}

export function teardown(data) {
  for (const schedule of [...(data?.todaySchedules || []), data?.futureSchedule]) {
    if (schedule?.createdByTest) {
      const accepted = cancelWorkSchedule(data.admin, schedule);
      check(accepted, { 'mixed test work schedule cleanup accepted': (value) => value === true });
    }
  }
}

function safeMetric(metrics, name) {
  const source = metrics[name];
  if (!source) return null;
  const values = source.values || source;
  const picked = {};
  for (const key of ['count', 'rate', 'value', 'avg', 'med', 'min', 'max', 'p(90)', 'p(95)', 'p(99)', 'passes', 'fails']) {
    if (values[key] !== undefined) picked[key] = values[key];
  }
  if (source.thresholds) picked.thresholds = source.thresholds;
  return picked;
}

export function handleSummary(data) {
  const names = [
    'mixed_read_requests', 'mixed_write_requests', 'mixed_read_success_rate', 'mixed_write_success_rate',
    'mixed_workflow_success_rate', 'mixed_server_error_rate', 'mixed_read_api_duration',
    'mixed_write_api_duration', 'mixed_booking_create_duration', 'mixed_queue_workflow_duration',
    'mixed_read_journeys', 'mixed_booking_journeys', 'mixed_queue_journeys', 'mixed_booking_confirmed',
    'mixed_checkins_completed', 'mixed_queue_lifecycle_completed', 'http_req_duration', 'http_req_failed',
  ];
  const summary = {
    profile: PROFILE,
    baseUrl: BASE_URL,
    vus: VUS,
    rampDuration: RAMP_DURATION,
    sustainedDuration: DURATION,
    metrics: Object.fromEntries(names.map((name) => [name, safeMetric(data.metrics, name)])),
  };
  return {
    stdout: `${JSON.stringify(summary, null, 2)}\n`,
    [`/results/mixed-${PROFILE}-summary.json`]: JSON.stringify(summary, null, 2),
  };
}
