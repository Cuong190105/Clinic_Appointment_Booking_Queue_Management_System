// Contention invariant: N *different* patients try to book the same slot at the same instant.
// Exactly one must win, all others must get 409, no 5xx, and the database must hold exactly one
// active appointment for the slot (verified through the staff API in teardown).
import { check, fail } from 'k6';
import { Counter } from 'k6/metrics';
import exec from 'k6/execution';
import { ADMIN_PASSWORD, ADMIN_USERNAME, clinicDate } from './config.js';
import { post } from './lib/api.js';
import { cleanupSchedules, ensureShift, listAppointmentsByDate, SEED_DOCTOR_IDS } from './lib/clinic.js';
import { mustLogin, patientCredentials } from './lib/session.js';
import { summarize } from './lib/summary.js';

const N = Number(__ENV.CONTENTION_VUS || 50);
const winners = new Counter('contention_winners');
const conflicts = new Counter('contention_conflicts');
const serverErrors = new Counter('contention_server_errors');

export const options = {
  setupTimeout: '5m',
  scenarios: { hot_slot: { executor: 'per-vu-iterations', vus: N, iterations: 1, maxDuration: '2m' } },
  thresholds: {
    contention_winners: ['count==1'],
    contention_conflicts: [`count==${N - 1}`],
    contention_server_errors: ['count==0'],
  },
};

export function setup() {
  if (!Number.isInteger(N) || N < 2) fail('CONTENTION_VUS must be an integer >= 2.');
  const admin = mustLogin({ username: ADMIN_USERNAME, password: ADMIN_PASSWORD });
  const date = clinicDate(5);
  const { schedule, slots } = ensureShift(admin, SEED_DOCTOR_IDS[0], date);
  if (!schedule || slots.length === 0) fail(`No open slot for contention on ${date}.`);
  // Log every contender in before the race so the race itself is only the booking POST.
  const tokens = [];
  for (let i = 0; i < N; i++) tokens.push(mustLogin(patientCredentials(i)));
  if (new Set(Array.from({ length: N }, (_, i) => patientCredentials(i).username)).size < N) {
    console.warn('[CONTENTION] Fewer distinct patients than VUs; provision a pool (LOAD_USER_POOL) for a true multi-patient race.');
  }
  return { admin, schedule, slot: slots[Math.floor(slots.length / 2)], tokens, runId: `contention-${Date.now()}` };
}

export default function (data) {
  const i = exec.vu.idInTest - 1;
  const res = post('/appointments', {
    token: data.tokens[i], key: `${data.runId}-${i}`, name: 'write-hot-slot', allowed: [200, 201, 409],
    body: { workScheduleId: data.schedule.id, timeSlot: data.slot.timeSlot, reason: `${data.runId} contender ${i}` },
  });
  if (res.status === 200 || res.status === 201) winners.add(1);
  else if (res.status === 409) conflicts.add(1);
  else if (res.status >= 500 || res.status === 0) serverErrors.add(1);
  check(res, { 'hot slot: created or 409': (r) => [200, 201, 409].includes(r.status) });
}

export function teardown(data) {
  const mine = listAppointmentsByDate(data.admin, data.schedule.date)
    .filter((a) => String(a.reason || '').startsWith(data.runId) && a.status !== 'Cancelled');
  check(mine, { 'DB holds exactly one active appointment for the contested slot': (m) => m.length === 1 });
  for (const a of mine) post(`/appointments/${a.id}/cancel`, { token: data.admin, cls: 'none', name: 'contention-cleanup', allowed: [200, 204] });
  cleanupSchedules(data.admin, [data.schedule]);
  if (mine.length !== 1) fail(`Contention invariant violated: ${mine.length} active appointments for ${data.slot.timeSlot}.`);
}

export function handleSummary(data) {
  return summarize('contention', data, { contenders: N });
}
