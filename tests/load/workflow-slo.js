// Synthetic probe for the asynchronous workflow SLOs (SLI_SLO_MATRIX §2.4). Low, steady traffic;
// each probe VU uses its own patient so inbox counts are not disturbed by other VUs.
//   booking POST -> Confirmed                 p95 <= 5 s
//   POST -> unread notification count +1     p95 <= 10 s
//   walk-in -> queue ticket visible            p95 <= 3 s
//   start-exam -> medical draft accepted       p95 <= 5 s
//   complete-exam -> appointment Completed     p95 <= 5 s
// Leaves Completed walk-in visits for today (terminal by design); online probe bookings are cancelled.
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';
import { apiThresholds, workflowThresholds } from './lib/slo.js';
import { post } from './lib/api.js';
import { patientBook, think, unreadNotifications, walkInVisit, workflow } from './lib/clinic.js';
import { setupMix, teardownMix } from './lib/mix.js';
import { doctorCredentials, patientCredentials, patientIdFor, receptionistCredentials, tokenFor } from './lib/session.js';
import { summarize } from './lib/summary.js';

const PROBES = Number(__ENV.PROBE_VUS || 2);
const DURATION = __ENV.PROBE_DURATION || '10m';
// Offset so probe patients do not overlap with the first accounts used by other tests.
const PATIENT_OFFSET = Number(__ENV.PROBE_PATIENT_OFFSET || 20);

export const options = {
  scenarios: { probe: { executor: 'constant-vus', vus: PROBES, duration: DURATION } },
  thresholds: { ...apiThresholds(), ...workflowThresholds() },
};

export function setup() {
  return setupMix({ minimumSlots: 50, withQueue: true, queueDoctors: PROBES });
}

const inboxSupport = new Rate('workflow_inbox_supported');

function bookingProbe(data, token) {
  const before = unreadNotifications(token);
  inboxSupport.add(before !== null);
  const started = Date.now();
  const booking = patientBook(token, data.slots, { runId: data.runId, cancel: false });
  if (!booking.created) return booking.conflict; // a 409 is a valid outcome, not a workflow failure

  // Systems without a persisted inbox (monolith) report the inbox SLO as not applicable.
  let inboxOk = before === null;
  if (booking.confirmed && before !== null) {
    const deadline = Date.now() + 20000;
    while (!inboxOk && Date.now() < deadline) {
      inboxOk = unreadNotifications(token) > before;
      if (!inboxOk) sleep(0.5);
    }
    if (inboxOk) workflow.notificationInbox.add(Date.now() - started);
  }
  // Patient cancels the probe booking (≥ 3 days ahead, so the 24 h rule allows it).
  post(`/appointments/${booking.appointmentId}/cancel`, { token, name: 'write-booking-cancel', allowed: [200, 204] });
  if (before !== null) check(inboxOk, { 'confirmation reached the patient inbox': (v) => v === true });
  return booking.confirmed && inboxOk;
}

function visitProbe(data, patient) {
  const actor = data.actors[(__VU - 1) % data.actors.length];
  const receptionist = tokenFor(receptionistCredentials(__VU - 1));
  const doctor = tokenFor(doctorCredentials(actor.index));
  const patientId = patientIdFor(patient);
  if (!receptionist || !doctor || !patientId) return false;
  const ok = walkInVisit(receptionist, doctor, actor, patientId, { runId: data.runId, full: true, recordWorkflow: true });
  check(ok, { 'walk-in visit completed end-to-end': (v) => v === true });
  return ok;
}

export default function (data) {
  const patient = patientCredentials(PATIENT_OFFSET + __VU - 1);
  const token = tokenFor(patient);
  if (!token) {
    workflow.success.add(false);
    return;
  }
  workflow.success.add(bookingProbe(data, token));
  workflow.success.add(visitProbe(data, patient));
  think(3, 6);
}

export function teardown(data) {
  teardownMix(data);
}

export function handleSummary(data) {
  const supported = data.metrics.workflow_inbox_supported?.values?.rate;
  return summarize('workflow-slo', data, { probes: PROBES, duration: DURATION, inboxSlo: supported > 0 ? 'measured' : 'not applicable (no persisted inbox)' });
}
