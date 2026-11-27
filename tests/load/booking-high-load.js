// Sustained booking write stress (open model). Distinct patients book random future slots at a
// fixed rate; 409 is a valid business outcome. Unlike the old closed-loop version, the slot pool is
// sized to stay well above demand so the run does not degrade into a 409-only test, and every
// booking created by the run is cancelled in teardown.
import { check } from 'k6';
import { apiThresholds, SLO } from './lib/slo.js';
import { cleanupRunAppointments, cleanupSchedules, patientBook, provisionSlotPool, think, workflow } from './lib/clinic.js';
import { THINK, vusFor } from './lib/mix.js';
import { mustLogin, patientCredentials, tokenFor } from './lib/session.js';
import { ADMIN_PASSWORD, ADMIN_USERNAME, clinicDate } from './config.js';
import { summarize } from './lib/summary.js';

const RATE = Number(__ENV.HIGH_LOAD_RATE || 20);
const DURATION_S = Number(__ENV.HIGH_LOAD_SECONDS || 120);
const DAY_OFFSETS = (__ENV.DAY_OFFSETS || '3,4,5,6,7,8,9,10').split(',').map(Number);
// Keep expected bookings below this share of the pool so slot exhaustion never dominates the result.
const MAX_POOL_FILL = Number(__ENV.HIGH_LOAD_MAX_POOL_FILL || 0.5);

export const options = {
  teardownTimeout: '10m',
  scenarios: {
    booking: { executor: 'constant-arrival-rate', rate: RATE, timeUnit: '1s', duration: `${DURATION_S}s`, ...vusFor(RATE) },
  },
  thresholds: {
    ...apiThresholds({ includeLogin: false }),
    workflow_booking_confirmed_duration: [`p(95)<${SLO.workflow.bookingConfirmedP95}`],
    // Most attempts must create a booking; a run dominated by 409s means the fixture is too small.
    booking_created_rate: ['rate>0.7'],
    dropped_iterations: [`count<${RATE * 5}`],
  },
};

export function setup() {
  const admin = mustLogin({ username: ADMIN_USERNAME, password: ADMIN_PASSWORD });
  const expected = RATE * DURATION_S;
  const { schedules, slots } = provisionSlotPool(admin, { dayOffsets: DAY_OFFSETS, minimumSlots: Math.ceil(expected / MAX_POOL_FILL) });
  console.log(`[HIGH-LOAD] rate=${RATE}/s duration=${DURATION_S}s expected attempts=${expected} slots=${slots.length}`);
  return { admin, schedules, slots, runId: `hl-${Date.now()}` };
}

export default function (data) {
  const token = tokenFor(patientCredentials(__VU - 1));
  if (!token) return;
  const result = patientBook(token, data.slots, { runId: data.runId, cancel: false, confirmTimeoutMs: 15000 });
  check(result, { 'created booking reached Confirmed': (r) => !r.created || r.confirmed });
  think(THINK.min, THINK.max);
}

export function teardown(data) {
  const cleanup = cleanupRunAppointments(data.admin, DAY_OFFSETS.map((d) => clinicDate(d)), data.runId);
  console.log(`[HIGH-LOAD CLEANUP] cancelled=${cleanup.cancelled} failed=${cleanup.failed}`);
  cleanupSchedules(data.admin, data.schedules);
}

export function handleSummary(data) {
  void workflow;
  return summarize('booking-high-load', data, { rate: RATE, durationSeconds: DURATION_S, dayOffsets: DAY_OFFSETS });
}
