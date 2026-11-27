// User-behaviour model (closed model, VUs = concurrent users). Each VU is a distinct account with
// a role (patient / receptionist / doctor) and human think time between actions; per-user rate
// limits stay at production values. Profiles:
//   normal      200 users, think 5–20 s  (ordinary clinic hours)
//   clinic-peak 500 users, think 3–15 s  (morning rush, walk-ins + queue lifecycle)
//   many-users  1500 users, think 15–45 s (thousands of concurrent sessions, mostly idle readers)
// Users > provisioned pool size reuse accounts (modulo); provision with scripts/provision-load-users.mjs.
import { check } from 'k6';
import { apiThresholds } from './lib/slo.js';
import { patientBook, patientBrowse, receptionistBoard, SEED_DOCTOR_IDS, think, walkInVisit } from './lib/clinic.js';
import { setupMix, teardownMix } from './lib/mix.js';
import { doctorCredentials, patientCredentials, patientIdFor, POOL_SIZE, receptionistCredentials, tokenFor } from './lib/session.js';
import { get } from './lib/api.js';
import { clinicDate } from './config.js';
import { summarize } from './lib/summary.js';

const PROFILES = {
  normal: { users: 200, ramp: '2m', hold: '10m', think: [5, 20], booking: 0.15, walkIn: 0 },
  'clinic-peak': { users: 500, ramp: '3m', hold: '10m', think: [3, 15], booking: 0.2, walkIn: 0.15 },
  'many-users': { users: 1500, ramp: '5m', hold: '15m', think: [15, 45], booking: 0.08, walkIn: 0.05 },
};
const PROFILE = String(__ENV.MIXED_PROFILE || 'normal').toLowerCase();
const P = PROFILES[PROFILE];
if (!P) throw new Error(`MIXED_PROFILE must be one of ${Object.keys(PROFILES).join(', ')}.`);
const USERS = Number(__ENV.MIXED_USERS || P.users);
const RAMP = __ENV.MIXED_RAMP || P.ramp;
const HOLD = __ENV.MIXED_DURATION || P.hold;
// 1 receptionist screen per 25 users, 1 doctor per 40 users.
const RECEPTION_EVERY = 25;
const DOCTOR_EVERY = 40;

export const options = {
  teardownTimeout: '5m',
  scenarios: {
    users: {
      executor: 'ramping-vus', startVUs: 0, gracefulRampDown: '60s',
      stages: [{ duration: RAMP, target: USERS }, { duration: HOLD, target: USERS }, { duration: '1m', target: 0 }],
    },
  },
  thresholds: {
    ...apiThresholds(),
    // Well-behaved distinct users must never be throttled by the per-user quotas.
    slo_throttled: ['rate<0.001'],
  },
};

function roleOf(vu) {
  if (vu % RECEPTION_EVERY === 0) return 'receptionist';
  if (vu % DOCTOR_EVERY === 1) return 'doctor';
  return 'patient';
}

export function setup() {
  if (POOL_SIZE > 0 && POOL_SIZE < USERS) console.warn(`[MIXED] ${USERS} users share ${POOL_SIZE} accounts; per-user quotas may be hit.`);
  if (POOL_SIZE === 0 && USERS > 30) console.warn('[MIXED] Using only the 30 seeded patients; provision a pool for realistic per-user behaviour.');
  return setupMix({ minimumSlots: 300, withQueue: P.walkIn > 0, queueDoctors: 5 });
}

export default function (data) {
  const vu = __VU;
  const role = roleOf(vu);

  if (role === 'patient') {
    const patient = patientCredentials(vu - 1);
    const token = tokenFor(patient);
    check(token, { 'patient session available': (t) => Boolean(t) });
    if (!token) return think(...P.think);
    if (Math.random() < P.booking) patientBook(token, data.slots, { runId: data.runId, cancel: true });
    else patientBrowse(token);
  } else if (role === 'receptionist') {
    const token = tokenFor(receptionistCredentials(vu));
    if (!token) return think(...P.think);
    if (data.actors.length && Math.random() < P.walkIn) {
      const actor = data.actors[vu % data.actors.length];
      const patient = patientCredentials(vu);
      const patientId = patientIdFor(patient);
      const doctorToken = tokenFor(doctorCredentials(actor.index));
      if (patientId && doctorToken) walkInVisit(token, doctorToken, actor, patientId, { runId: data.runId });
    } else {
      receptionistBoard(token);
    }
  } else {
    // doctorN owns seeded doctor id N; each doctor screen polls its own queue.
    const index = Math.floor(vu / DOCTOR_EVERY) % SEED_DOCTOR_IDS.length;
    const token = tokenFor(doctorCredentials(index));
    if (token) get(`/doctors/${SEED_DOCTOR_IDS[index]}/queue?date=${clinicDate(0)}`, { token, name: 'read-doctor-queue' });
  }
  think(...P.think);
}

export function teardown(data) {
  teardownMix(data);
}

export function handleSummary(data) {
  return summarize(`mixed-${PROFILE}`, data, { users: USERS, ramp: RAMP, hold: HOLD, think: P.think, poolSize: POOL_SIZE });
}
