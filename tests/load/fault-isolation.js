// Degraded-mode verification (SLI_SLO_MATRIX §5.4). Driven by scripts/run-fault-isolation.ps1, which
// stops FAULT_TARGET at FAULT_DOWN_AT_S and starts it again at FAULT_UP_AT_S while this script runs.
// Each request is tagged with the phase it ran in; gates are evaluated per phase:
//   before    normal SLOs
//   degraded  journeys that do not depend on the stopped service keep their SLOs; journeys that do
//             depend on it must fail FAST (503/504/502 within the 5 s deadline, never 500)
//   recovery  normal SLOs again (circuit half-open -> closed) after FAULT_RECOVERY_GRACE_S
import { sleep } from 'k6';
import exec from 'k6/execution';
import { Counter, Rate, Trend } from 'k6/metrics';
import { clinicDate } from './config.js';
import { get, post } from './lib/api.js';
import { cleanupRunAppointments, patientBook, pick, SEED_DOCTOR_IDS, think } from './lib/clinic.js';
import { setupMix, teardownMix } from './lib/mix.js';
import { patientCredentials, tokenFor } from './lib/session.js';
import { SLO } from './lib/slo.js';
import { summarize } from './lib/summary.js';

const TARGET = __ENV.FAULT_TARGET || 'notification-service';
const DOWN_AT = Number(__ENV.FAULT_DOWN_AT_S || 60);
const UP_AT = Number(__ENV.FAULT_UP_AT_S || 150);
const TOTAL = Number(__ENV.FAULT_TOTAL_S || 240);
const BREAKER_GRACE = Number(__ENV.FAULT_DEGRADED_GRACE_S || 15);
const RECOVERY_GRACE = Number(__ENV.FAULT_RECOVERY_GRACE_S || 30);
const USERS = Number(__ENV.FAULT_USERS || 30);

// Which journeys depend on the stopped service.
const DEPENDS = {
  'notification-service': { booking: false, scheduling: false },
  'medical-records-service': { booking: false, scheduling: false },
  'identity-service': { booking: false, scheduling: false },
  'scheduling-service': { booking: true, scheduling: true },
}[TARGET];
if (!DEPENDS) throw new Error(`Unsupported FAULT_TARGET ${TARGET}.`);

// Among dependent requests that FAILED while degraded: share that failed explicitly (502/503/504) and fast.
const failFast = new Rate('fault_dependent_fail_fast');
const failFastDuration = new Trend('fault_dependent_failure_duration', true);
const dependentFailures = new Counter('fault_dependent_failures');

export const options = {
  setupTimeout: '3m',
  teardownTimeout: '5m',
  scenarios: { users: { executor: 'constant-vus', vus: USERS, duration: `${TOTAL}s` } },
  thresholds: {
    'slo_availability{phase:before}': [`rate>=${SLO.availability}`],
    'slo_write_duration{phase:before}': [`p(95)<${SLO.write.p95}`],
    'slo_availability{phase:degraded}': [`rate>=${SLO.availability}`],
    'slo_write_duration{phase:degraded}': [`p(95)<${SLO.write.p95}`],
    'slo_read_duration{phase:degraded}': [`p(95)<${SLO.read.p95}`],
    'slo_availability{phase:recovery}': [`rate>=${SLO.availability}`],
    // Journeys that never depend on the stopped service must stay available in EVERY phase, including
    // the transitions; this catches cascades such as a breaker on a healthy service opening.
    'slo_availability{journey:core}': [`rate>=${SLO.availability}`],
    'slo_write_duration{phase:recovery}': [`p(95)<${SLO.write.p95}`],
    ...(DEPENDS.booking ? {
      fault_dependent_fail_fast: ['rate>=0.99'],
      fault_dependent_failure_duration: ['p(95)<5500'],
      // The outage must actually have been observed, otherwise the run proves nothing.
      fault_dependent_failures: ['count>0'],
    } : {}),
  },
};

export function setup() {
  // Identity outage: VUs must hold tokens issued before the outage (no refresh during the run).
  return { ...setupMix({ minimumSlots: 200, withQueue: false }), startedAt: Date.now() };
}

function phaseAt(elapsedS) {
  if (elapsedS < DOWN_AT) return 'before';
  if (elapsedS >= DOWN_AT + BREAKER_GRACE && elapsedS < UP_AT) return 'degraded';
  if (elapsedS >= UP_AT + RECOVERY_GRACE) return 'recovery';
  return 'transition';
}

export default function (data) {
  // Users start over the first 15 s instead of all logging in at once (Identity handles ~3 logins/s
  // at 0.5 CPU, SLI_SLO_MATRIX §2.3); the outage starts well after all sessions exist.
  if (__ITER === 0) sleep(Math.random() * 15);
  const elapsed = (Date.now() - data.startedAt) / 1000;
  const phase = phaseAt(elapsed);
  exec.vu.tags.phase = phase;
  const token = tokenFor(patientCredentials(__VU - 1));
  if (!token) return think(3, 8);

  const degradedDependency = phase === 'degraded' && DEPENDS.booking;
  if (Math.random() < 0.3) {
    if (degradedDependency) {
      // Booking depends on the stopped Scheduling service: it must fail fast and explicitly.
      const started = Date.now();
      const outcome = degradedBooking(token, data);
      const failed = outcome.status === 0 || outcome.status >= 500;
      if (failed) {
        dependentFailures.add(1);
        const took = Date.now() - started;
        failFastDuration.add(took);
        failFast.add([502, 503, 504].includes(outcome.status) && took <= 5500);
      }
    } else {
      patientBook(token, data.slots, { runId: data.runId, cancel: true });
    }
  } else {
    // Core reads never depend on the stopped service (served by Gateway + Appointment/Queue) and are
    // tagged journey=core; the availability read depends on Scheduling and is only used when it is up.
    const core = [
      () => get('/me/appointments?category=upcoming', { token, name: 'read-my-appointments' }),
      () => get('/me/queue-status', { token, name: 'read-my-queue-status', allowed: [200, 204] }),
    ];
    const schedulingMayBeDown = DEPENDS.scheduling && phase !== 'before' && phase !== 'recovery';
    if (!schedulingMayBeDown && Math.random() < 0.33) {
      get(`/doctors/${pick(SEED_DOCTOR_IDS)}/available?date=${clinicDate(3)}`, { token, name: 'read-availability' });
    } else {
      exec.vu.tags.journey = 'core';
      pick(core)();
      delete exec.vu.tags.journey;
    }
  }
  think(3, 8);
}

// Booking attempt while its dependency is down: expected to fail fast, so excluded from SLO metrics.
function degradedBooking(token, data) {
  const slot = pick(data.slots);
  return post('/appointments', {
    token, slo: false, name: 'degraded-booking', allowed: [200, 201, 409, 502, 503, 504],
    key: `${data.runId}-deg-${__VU}-${__ITER}`,
    body: { workScheduleId: slot.scheduleId, timeSlot: slot.timeSlot, reason: `Fault ${data.runId}` },
  });
}

export function teardown(data) {
  // Bookings that succeeded around the outage boundaries were not cancelled inline.
  const dates = [...new Set(data.slots.map((slot) => slot.date))];
  const cleanup = cleanupRunAppointments(data.admin, dates, data.runId);
  console.log(`[FAULT CLEANUP] cancelled=${cleanup.cancelled} failed=${cleanup.failed}`);
  teardownMix(data);
}

export function handleSummary(data) {
  return summarize(`fault-${TARGET}`, data, { target: TARGET, downAtS: DOWN_AT, upAtS: UP_AT, totalS: TOTAL });
}
