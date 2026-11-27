// Weighted transaction mix for open-model (arrival-rate) tests. One iteration = one user action,
// so the offered load (iterations/s) is identical for monolith and microservices runs.
import { ADMIN_PASSWORD, ADMIN_USERNAME } from '../config.js';
import { cleanupSchedules, doctorActor, patientBook, patientBrowse, provisionSlotPool, provisionTodayQueue, receptionistBoard, think, walkInVisit } from './clinic.js';
import { doctorCredentials, mustLogin, patientCredentials, patientIdFor, receptionistCredentials, tokenFor } from './session.js';

const ENV = __ENV;

// Human think time after each action. Under an arrival-rate executor this does not change the
// offered rate; it makes k6 spread the load over more VUs (= more distinct users), each acting at
// a realistic pace that stays within the per-user rate limits (§5.3).
export const THINK = { min: Number(ENV.THINK_MIN_S ?? 3), max: Number(ENV.THINK_MAX_S ?? 10) };

/** VU budget for an arrival rate given think time (≈ concurrent users needed). */
// Little's law: concurrent VUs ≈ rate × iteration time (average think + ~3 s of requests). Pre-allocate
// that many so k6 never has to initialise VUs mid-run (which drops iterations), with headroom to max.
export function vusFor(rate) {
  const avgIterationS = (THINK.min + THINK.max) / 2 + 3;
  return {
    preAllocatedVUs: Math.max(10, Math.ceil(rate * avgIterationS * 1.2)),
    maxVUs: Math.max(50, Math.ceil(rate * (THINK.max + 8))),
  };
}

// Default mix ≈ 70% reads / 30% writes by request count (clinic usage assumption, §6).
export const DEFAULT_WEIGHTS = {
  patientRead: Number(ENV.MIX_PATIENT_READ ?? 0.6),
  booking: Number(ENV.MIX_BOOKING ?? 0.2),
  staffRead: Number(ENV.MIX_STAFF_READ ?? 0.15),
  walkIn: Number(ENV.MIX_WALK_IN ?? 0.05),
};

export function setupMix({ minimumSlots = 200, dayOffsets = (ENV.DAY_OFFSETS || '3,4,5,6,7').split(',').map(Number), withQueue = DEFAULT_WEIGHTS.walkIn > 0, queueDoctors = 3 } = {}) {
  const admin = mustLogin({ username: ADMIN_USERNAME, password: ADMIN_PASSWORD });
  const { schedules, slots } = provisionSlotPool(admin, { dayOffsets, minimumSlots });
  const actors = [];
  let todaySchedules = [];
  if (withQueue) {
    for (let i = 0; i < queueDoctors; i++) actors.push({ index: i, ...doctorActor(mustLogin(doctorCredentials(i))) });
    todaySchedules = provisionTodayQueue(admin, actors);
  }
  return { admin, schedules, slots, actors, todaySchedules, runId: `${ENV.RUN_ID || 'run'}-${Date.now()}` };
}

export function teardownMix(data) {
  cleanupSchedules(data.admin, [...(data.schedules || []), ...(data.todaySchedules || [])]);
}

/** Executes one weighted user action. Returns the action name. */
export function runAction(data, weights = DEFAULT_WEIGHTS, { userIndex = __VU - 1, cancelBookings = true } = {}) {
  const patient = patientCredentials(userIndex);
  const total = Object.values(weights).reduce((a, b) => a + b, 0);
  let r = Math.random() * total;
  const choose = (w) => (r -= w) < 0;

  if (choose(weights.patientRead)) {
    const token = tokenFor(patient);
    if (token) patientBrowse(token);
    return 'patientRead';
  }
  if (choose(weights.booking)) {
    const token = tokenFor(patient);
    if (token) patientBook(token, data.slots, { runId: data.runId, cancel: cancelBookings });
    return 'booking';
  }
  if (choose(weights.staffRead)) {
    const token = tokenFor(receptionistCredentials(userIndex));
    if (token) receptionistBoard(token);
    return 'staffRead';
  }
  if (data.actors?.length) {
    const actor = data.actors[userIndex % data.actors.length];
    const receptionist = tokenFor(receptionistCredentials(userIndex));
    const doctor = tokenFor(doctorCredentials(actor.index));
    const patientId = patientIdFor(patient);
    if (receptionist && doctor && patientId) walkInVisit(receptionist, doctor, actor, patientId, { runId: data.runId });
  }
  return 'walkIn';
}

/** One action followed by think time (default iteration body for open-model scripts). */
export function act(data, weights = DEFAULT_WEIGHTS) {
  const name = runAction(data, weights);
  think(THINK.min, THINK.max);
  return name;
}
