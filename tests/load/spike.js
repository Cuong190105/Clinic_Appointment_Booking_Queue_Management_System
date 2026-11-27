// Booking-opening spike: many *distinct* patients hit a few popular doctors at once (e.g. the
// morning a new week of slots opens). Gates: no 5xx above 1%, availability >= 99%, 409 is a valid
// outcome, write p95 <= 1s during the spike and back within the normal SLO in the recovery phase.
//
// Sessions are warmed in setup() at a paced login rate: in a real opening rush most patients are
// already signed in (15-min access token, 7-day refresh token). Login capacity under a burst is a
// separate question, measured by login-capacity.js. Set SPIKE_WARM_SESSIONS=0 to reproduce the old
// cold-login behaviour.
import { apiThresholds, SLO } from './lib/slo.js';
import { cleanupRunAppointments, cleanupSchedules, patientBook, patientBrowse, provisionSlotPool, SEED_DOCTOR_IDS, think } from './lib/clinic.js';
import { THINK, vusFor } from './lib/mix.js';
import { adoptSessions, mustLogin, patientCredentials, tokenFor, warmSessions } from './lib/session.js';
import { ADMIN_PASSWORD, ADMIN_USERNAME } from './config.js';
import { summarize } from './lib/summary.js';

const BASE = Number(__ENV.SPIKE_BASE_RATE || 3);
const PEAK = Number(__ENV.SPIKE_RATE || 40);
const HOT_DOCTORS = Number(__ENV.SPIKE_HOT_DOCTORS || 2);
const WARM = __ENV.SPIKE_WARM_SESSIONS !== '0';
// VU ids are global across scenarios; warm every pre-allocated VU's account. VUs added beyond the
// pre-allocation log in lazily, like newly arriving users.
const WARM_USERS = Number(__ENV.SPIKE_WARM_USERS || (2 * vusFor(BASE).preAllocatedVUs + vusFor(PEAK).preAllocatedVUs));

export const options = {
  setupTimeout: '20m',
  teardownTimeout: '5m',
  scenarios: {
    baseline: { executor: 'constant-arrival-rate', rate: BASE, timeUnit: '1s', duration: '30s', ...vusFor(BASE), tags: { phase: 'baseline' } },
    spike: {
      executor: 'ramping-arrival-rate', startRate: BASE, timeUnit: '1s', startTime: '30s',
      stages: [{ target: PEAK, duration: '10s' }, { target: PEAK, duration: '60s' }, { target: BASE, duration: '10s' }],
      ...vusFor(PEAK), tags: { phase: 'spike' },
    },
    recovery: { executor: 'constant-arrival-rate', rate: BASE, timeUnit: '1s', startTime: '140s', duration: '60s', ...vusFor(BASE), tags: { phase: 'recovery' } },
  },
  thresholds: {
    ...apiThresholds({ writeP95: 1000, readP95: 1000 }),
    'slo_write_duration{phase:recovery}': [`p(95)<${SLO.write.p95}`],
    'slo_read_duration{phase:recovery}': [`p(95)<${SLO.read.p95}`],
  },
};

export function setup() {
  const admin = mustLogin({ username: ADMIN_USERNAME, password: ADMIN_PASSWORD });
  const { schedules, slots } = provisionSlotPool(admin, { doctorIds: SEED_DOCTOR_IDS.slice(0, HOT_DOCTORS), dayOffsets: [3, 4], minimumSlots: 50 });
  const warm = WARM ? warmSessions(Array.from({ length: WARM_USERS }, (_, i) => patientCredentials(i))) : null;
  if (warm) console.log(`[SPIKE] warmed ${Object.keys(warm.tokens).length} patient sessions`);
  return { admin, schedules, slots, warm, runId: `spike-${Date.now()}` };
}

export default function (data) {
  // Each VU is a different patient account, as in a real opening rush.
  adoptSessions(data.warm);
  const token = tokenFor(patientCredentials(__VU - 1));
  if (!token) return;
  if (Math.random() < 0.6) patientBook(token, data.slots, { runId: data.runId, cancel: false, confirmTimeoutMs: 8000 });
  else patientBrowse(token, SEED_DOCTOR_IDS.slice(0, HOT_DOCTORS));
  think(THINK.min, THINK.max);
}

export function teardown(data) {
  const cleanup = cleanupRunAppointments(data.admin, data.schedules.map((s) => s.date), data.runId);
  console.log(`[SPIKE CLEANUP] cancelled=${cleanup.cancelled} failed=${cleanup.failed}`);
  cleanupSchedules(data.admin, data.schedules);
}

export function handleSummary(data) {
  return summarize('spike', data, { baseRate: BASE, peakRate: PEAK, hotDoctors: HOT_DOCTORS, warmSessions: WARM ? WARM_USERS : 0 });
}
