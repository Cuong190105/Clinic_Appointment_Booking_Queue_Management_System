// Fixtures and realistic user journeys shared by the SLO test suite.
import { check, fail, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import { clinicDate, responseItems } from '../config.js';
import { get, idOf, json, post, patch } from './api.js';

const ENV = __ENV;
const CLINIC_OFFSET_MIN = Number(ENV.CLINIC_UTC_OFFSET_MINUTES || 420);

// Seeded doctors a0000004-…-0001 … -0010 (full-clean-seed.sql).
export const SEED_DOCTOR_IDS = Array.from({ length: Number(ENV.SEED_DOCTOR_COUNT || 10) }, (_, i) =>
  `a0000004-0000-0000-0000-${String(i + 1).padStart(12, '0')}`);

export const workflow = {
  bookingConfirmed: new Trend('workflow_booking_confirmed_duration', true),
  checkinTicket: new Trend('workflow_checkin_ticket_duration', true),
  examCompleted: new Trend('workflow_exam_completed_duration', true),
  encounterOpen: new Trend('workflow_encounter_open_duration', true),
  notificationInbox: new Trend('workflow_notification_inbox_duration', true),
  success: new Rate('workflow_success_rate'),
  bookingOutcome: new Rate('booking_created_rate'),
};

export function localMinutesNow() {
  const now = new Date(Date.now() + CLINIC_OFFSET_MIN * 60_000);
  return now.getUTCHours() * 60 + now.getUTCMinutes();
}

export function timeString(minutes) {
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}:00`;
}

export function timeMinutes(value) {
  const m = String(value || '').match(/(\d{2}):(\d{2})/);
  return m ? Number(m[1]) * 60 + Number(m[2]) : -1;
}

export function think(minS, maxS) {
  sleep(minS + Math.random() * Math.max(0, maxS - minS));
}

export function pick(list) {
  return list[Math.floor(Math.random() * list.length)];
}

// ---------------------------------------------------------------- fixtures

function availableSlots(doctorId, date, scheduleId) {
  const res = get(`/doctors/${doctorId}/available?date=${date}`, { name: 'fixture-availability', cls: 'none' });
  if (res.status !== 200) return [];
  const schedules = responseItems(JSON.parse(res.body), ['timeSlot', 'timeSlots']);
  const schedule = schedules.find((s) => s.id === scheduleId);
  const values = schedule?.timeSlot || schedule?.timeSlots || [];
  return values.map((timeSlot) => ({ scheduleId, doctorId, date, timeSlot }));
}

/** Creates (or reuses) one active shift per doctor/date and returns its open slots. */
export function ensureShift(adminToken, doctorId, date, start = '08:00:00', end = '20:00:00', capacity = 100) {
  const created = post(`/doctors/${doctorId}/shifts`, {
    token: adminToken, cls: 'none', name: 'fixture-create-shift', allowed: [200, 201, 400, 409],
    body: { date, startTime: start, endTime: end, recommendedCapacity: capacity },
  });
  let schedule = null;
  if (created.status === 200 || created.status === 201) {
    const body = json(created);
    if (body?.id) schedule = { id: body.id, doctorId, date, createdByTest: true };
  }
  if (!schedule) {
    const list = get(`/doctors/${doctorId}/shifts?startDate=${date}&endDate=${date}`, { token: adminToken, cls: 'none', name: 'fixture-find-shift' });
    const active = list.status === 200 ? responseItems(JSON.parse(list.body)).find((s) => s.status === 'Active' || s.status === 0) : null;
    if (active?.id) schedule = { id: active.id, doctorId, date, createdByTest: false };
  }
  if (!schedule) return { schedule: null, slots: [] };
  return { schedule, slots: availableSlots(doctorId, date, schedule.id) };
}

/** Slot pool across doctors × future days. Fails fast when the pool is too small for the planned load. */
export function provisionSlotPool(adminToken, { doctorIds = SEED_DOCTOR_IDS, dayOffsets = [3, 4, 5, 6, 7], minimumSlots = 1 } = {}) {
  const schedules = [];
  const slots = [];
  for (const offset of dayOffsets) {
    const date = clinicDate(offset);
    for (const doctorId of doctorIds) {
      const { schedule, slots: open } = ensureShift(adminToken, doctorId, date);
      if (schedule) schedules.push(schedule);
      slots.push(...open);
    }
  }
  if (slots.length < minimumSlots) {
    cleanupSchedules(adminToken, schedules);
    fail(`Slot pool has ${slots.length} open slots; ${minimumSlots} required for this load profile. Add DAY_OFFSETS/doctors or lower the rate.`);
  }
  return { schedules, slots };
}

/** Same-day shifts for walk-in / queue journeys (starts ≥ 20 min from now, ends 23:59). */
export function provisionTodayQueue(adminToken, doctorActors) {
  const first = Math.ceil((localMinutesNow() + 20) / 15) * 15;
  if (first >= 22 * 60) fail('Queue journeys need at least one same-day slot before 22:00 clinic time.');
  const schedules = [];
  for (const actor of doctorActors) {
    const { schedule } = ensureShift(adminToken, actor.doctorId, clinicDate(0), timeString(first), '23:59:00');
    if (schedule) schedules.push(schedule);
  }
  return schedules;
}

export function cleanupSchedules(adminToken, schedules) {
  let ok = true;
  for (const schedule of schedules || []) {
    if (!schedule?.createdByTest) continue;
    const res = post(`/shifts/${schedule.id}/cancel`, {
      token: adminToken, cls: 'none', name: 'fixture-cleanup', allowed: [200, 204],
      body: { reason: 'Automated load-test fixture cleanup' },
    });
    ok = ok && (res.status === 200 || res.status === 204);
  }
  return ok;
}

/** All appointments on a date via the Admin listing (paged; same route on monolith and microservices). */
export function listAppointmentsByDate(adminToken, date, pageSize = 100) { // API max page size is 100
  const all = [];
  for (let page = 1; page <= 100; page++) {
    const res = get(`/admin/appointments?date=${date}&pageNumber=${page}&pageSize=${pageSize}`, { token: adminToken, cls: 'none', name: 'admin-appointments-by-date' });
    if (res.status !== 200) fail(`Admin appointment listing failed for ${date}: HTTP ${res.status}`);
    const body = json(res);
    const items = Array.isArray(body) ? body : body?.items || [];
    all.push(...items);
    if (items.length < pageSize) break;
  }
  return all;
}

/**
 * Cancels every non-terminal appointment created by this run (matched by runId in the reason).
 * Needed because cancelling a shift does not cancel the appointments booked on it.
 */
export function cleanupRunAppointments(adminToken, dates, runId) {
  let cancelled = 0;
  let failed = 0;
  for (const date of [...new Set(dates)]) {
    for (const a of listAppointmentsByDate(adminToken, date)) {
      if (!String(a.reason || '').includes(runId) || ['Cancelled', 'Completed'].includes(a.status)) continue;
      const c = post(`/appointments/${a.id}/cancel`, { token: adminToken, cls: 'none', name: 'cleanup-cancel', allowed: [200, 204, 400] });
      if (c.status === 200 || c.status === 204) cancelled++; else failed++;
    }
  }
  return { cancelled, failed };
}

/** Resolves doctor id + specialty id for a doctor account (works for monolith and microservices). */
export function doctorActor(token) {
  const me = get('/doctors/me', { token, cls: 'none', name: 'doctor-me', allowed: [200] });
  const doctor = json(me);
  if (!doctor?.id) fail('Doctor profile lookup failed.');
  let specialtyId = doctor.specialtyId;
  if (!specialtyId) {
    const name = String(doctor.currentSpecialty || doctor.specialtyName || '').trim().toLowerCase();
    const catalog = get('/specialties?page=1&pageSize=100', { cls: 'none', name: 'specialty-catalog' });
    specialtyId = responseItems(JSON.parse(catalog.body), ['items'])
      .find((s) => String(s.name || '').trim().toLowerCase() === name)?.id;
  }
  if (!specialtyId) fail(`Could not resolve specialty for doctor ${doctor.id}.`);
  return { doctorId: doctor.id, specialtyId };
}

// ---------------------------------------------------------------- journeys

export function patientBrowse(token, doctorIds = SEED_DOCTOR_IDS) {
  const reads = [
    () => get('/specialties', { name: 'read-specialties' }),
    () => get('/doctors', { name: 'read-doctors' }),
    () => get(`/doctors/${pick(doctorIds)}/available?date=${clinicDate(1 + Math.floor(Math.random() * 6))}`, { name: 'read-availability' }),
    () => get('/me/appointments?category=upcoming', { token, name: 'read-my-appointments' }),
    () => get('/me/notifications', { token, name: 'read-my-notifications' }),
    () => get('/me/queue-status', { token, name: 'read-my-queue-status', allowed: [200, 204] }),
    () => get('/me/medical-history', { token, name: 'read-medical-history' }),
  ];
  return pick(reads)();
}

/**
 * Unread notification count, or null when the system has no persisted in-app inbox (the monolith
 * only exposes an empty /me/notifications and delivers through an in-memory channel).
 */
export function unreadNotifications(token) {
  const res = get('/notifications/unread-count', { token, name: 'read-unread-count', allowed: [200, 404] });
  if (res.status === 200) return Number(json(res)?.unreadCount ?? json(res)?.count ?? 0);
  return null;
}

/** Polls until predicate(res) is true; returns elapsed ms or null on timeout. */
export function pollUntil(fn, predicate, timeoutMs, intervalS = 0.25) {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    const res = fn();
    if (predicate(res)) return Date.now() - started;
    sleep(intervalS);
  }
  return null;
}

/**
 * Patient booking journey: book a random slot (409 is a valid business outcome), wait for the
 * async confirmation, optionally verify idempotent replay, then cancel as the owner.
 * Returns { created, confirmed, appointmentId }.
 */
export function patientBook(token, slotPool, { runId, cancel = true, verifyIdempotency = false, confirmTimeoutMs = 15000 } = {}) {
  const slot = pick(slotPool);
  const key = `${runId}-b-${__VU}-${__ITER}-${Math.floor(Math.random() * 1e9)}`;
  const body = { workScheduleId: slot.scheduleId, timeSlot: slot.timeSlot, reason: `Load ${runId}` };
  const postedAt = Date.now();
  const res = post('/appointments', { token, key, body, name: 'write-booking-create', allowed: [200, 201, 409] });
  const appointmentId = res.status === 200 || res.status === 201 ? idOf(res) : null;
  workflow.bookingOutcome.add(Boolean(appointmentId));
  if (!appointmentId) return { created: false, confirmed: false, appointmentId: null, conflict: res.status === 409 };

  const waited = pollUntil(
    () => get(`/appointments/${appointmentId}`, { token, name: 'read-booking-status' }),
    (r) => json(r)?.status === 'Confirmed',
    confirmTimeoutMs,
  );
  const confirmed = waited !== null;
  if (confirmed) workflow.bookingConfirmed.add(Date.now() - postedAt);

  if (verifyIdempotency) {
    const replay = post('/appointments', { token, key, body, name: 'write-booking-replay', allowed: [200, 201] });
    check(replay, { 'idempotent replay returns same appointment': (r) => idOf(r) === appointmentId });
  }
  if (cancel) {
    post(`/appointments/${appointmentId}/cancel`, { token, name: 'write-booking-cancel', allowed: [200, 204] });
  }
  return { created: true, confirmed, appointmentId, conflict: false };
}

export function receptionistBoard(token, doctorIds = SEED_DOCTOR_IDS) {
  const reads = [
    () => get('/receptionist/appointments/pending', { token, name: 'read-reception-pending' }),
    () => get('/receptionist/queue-doctors', { token, name: 'read-reception-queue-doctors' }),
    () => get(`/doctors/${pick(doctorIds)}/queue?date=${clinicDate(0)}`, { token, name: 'read-queue-board' }),
  ];
  return pick(reads)();
}

/**
 * Same-day visit: receptionist walk-in (atomic check-in) → queue ticket appears → call.
 * full=true continues start-exam → complete-exam → appointment Completed (+ encounter draft),
 * full=false skips/retries and cancels so the run leaves no residue.
 */
export function walkInVisit(receptionistToken, doctorToken, actor, patientId, { runId, full = false, recordWorkflow = false } = {}) {
  const key = `${runId}-w-${__VU}-${__ITER}-${Math.floor(Math.random() * 1e9)}`;
  const createdAt = Date.now();
  const walkIn = post('/receptionist/appointments/walk-in', {
    token: receptionistToken, key, name: 'write-walk-in', allowed: [200, 201],
    body: { patientId, specialtyId: actor.specialtyId, doctorId: actor.doctorId, reason: `Walk-in ${runId}`, isUrgent: false },
  });
  const appointmentId = idOf(walkIn);
  if (!appointmentId) return false;

  let ticketId = null;
  const ticketMs = pollUntil(
    () => get(`/queue/appointment/${appointmentId}`, { token: receptionistToken, name: 'read-queue-ticket', allowed: [200, 404] }),
    (r) => {
      if (r.status !== 200) return false;
      ticketId = json(r)?.id || json(r)?.queueTicketId || null;
      return Boolean(ticketId);
    },
    10000,
    0.1,
  );
  if (ticketMs === null) return false;
  if (recordWorkflow) workflow.checkinTicket.add(Date.now() - createdAt);

  const call = post(`/queue/${ticketId}/call`, { token: receptionistToken, name: 'write-queue-call', allowed: [200] });
  if (call.status !== 200) return false;

  if (!full) {
    post(`/queue/${ticketId}/skip`, { token: doctorToken, name: 'write-queue-skip', allowed: [200] });
    post(`/queue/${ticketId}/retry`, { token: receptionistToken, name: 'write-queue-retry', allowed: [200] });
    post(`/appointments/${appointmentId}/cancel`, { token: receptionistToken, name: 'write-walk-in-cleanup', allowed: [200, 204] });
    return true;
  }

  const startedAt = Date.now();
  const start = post(`/queue/${ticketId}/start-exam`, { token: doctorToken, name: 'write-start-exam', allowed: [200, 204] });
  if (start.status !== 200 && start.status !== 204) return false;
  const encounterMs = pollUntil(
    () => post('/medical-reports', {
      token: doctorToken, name: 'write-medical-draft', allowed: [200, 201, 400, 404, 409],
      body: { appointmentId, symptoms: 'Load probe', diagnosis: 'N/A', notes: 'synthetic', prescription: '', isFinalize: false },
    }),
    (r) => r.status === 200 || r.status === 201,
    10000,
    0.25,
  );
  if (recordWorkflow && encounterMs !== null) workflow.encounterOpen.add(Date.now() - startedAt);

  const completedAt = Date.now();
  const complete = post(`/queue/${ticketId}/complete-exam`, { token: doctorToken, name: 'write-complete-exam', allowed: [200, 204] });
  if (complete.status !== 200 && complete.status !== 204) return false;
  const doneMs = pollUntil(
    () => get(`/appointments/${appointmentId}`, { token: receptionistToken, name: 'read-appointment-completed' }),
    (r) => json(r)?.status === 'Completed',
    10000,
  );
  if (recordWorkflow && doneMs !== null) workflow.examCompleted.add(Date.now() - completedAt);
  return encounterMs !== null && doneMs !== null;
}

export function staffPatch(path, token, body, name) {
  return patch(path, { token, body, name, allowed: [200, 204] });
}
