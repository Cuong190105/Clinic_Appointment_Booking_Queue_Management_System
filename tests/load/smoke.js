// Smoke: a few passes through every Tier-1 journey with the SLO gates of SLI_SLO_MATRIX §2.
// Run before any heavier test and after each deploy (§4.2). Non-zero exit if a journey or gate fails.
import { check } from 'k6';
import { ADMIN_PASSWORD, ADMIN_USERNAME } from './config.js';
import { apiThresholds } from './lib/slo.js';
import { cleanupSchedules, doctorActor, patientBook, patientBrowse, provisionSlotPool, provisionTodayQueue, receptionistBoard, walkInVisit } from './lib/clinic.js';
import { doctorCredentials, mustLogin, patientCredentials, patientIdFor, receptionistCredentials, tokenFor } from './lib/session.js';
import { summarize } from './lib/summary.js';

// Queue journey needs a same-day slot before 22:00 clinic time; disable with SMOKE_QUEUE=false.
const WITH_QUEUE = (__ENV.SMOKE_QUEUE || 'true') === 'true';

export const options = {
  scenarios: { smoke: { executor: 'per-vu-iterations', vus: 1, iterations: 3, maxDuration: '3m' } },
  thresholds: {
    ...apiThresholds(),
    slo_unexpected_status: ['rate==0'],
    checks: ['rate==1'],
  },
};

export function setup() {
  const admin = mustLogin({ username: ADMIN_USERNAME, password: ADMIN_PASSWORD });
  const { schedules, slots } = provisionSlotPool(admin, { dayOffsets: [3], minimumSlots: 10 });
  let queue = null;
  if (WITH_QUEUE) {
    const actor = doctorActor(mustLogin(doctorCredentials(0)));
    queue = { actor, todaySchedules: provisionTodayQueue(admin, [actor]) };
  }
  return { admin, schedules, slots, queue, runId: `smoke-${Date.now()}` };
}

export default function (data) {
  const patient = patientCredentials(__ITER);
  const token = tokenFor(patient);
  check(token, { 'patient login works': (t) => Boolean(t) });
  if (!token) return;

  patientBrowse(token);
  const booking = patientBook(token, data.slots, { runId: data.runId, verifyIdempotency: true });
  check(booking, {
    'booking created or slot conflict': (b) => b.created || b.conflict,
    'created booking reached Confirmed': (b) => !b.created || b.confirmed,
  });

  const receptionist = tokenFor(receptionistCredentials(0));
  receptionistBoard(receptionist);

  if (data.queue && __ITER === 0) {
    const ok = walkInVisit(receptionist, tokenFor(doctorCredentials(0)), data.queue.actor, patientIdFor(patient), { runId: data.runId });
    check(ok, { 'walk-in -> queue ticket -> call works': (v) => v === true });
  }
}

export function teardown(data) {
  cleanupSchedules(data.admin, [...data.schedules, ...(data.queue?.todaySchedules || [])]);
}

export function handleSummary(data) {
  return summarize('smoke', data);
}
