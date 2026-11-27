// Single source of truth for SLO gates used by every k6 script.
// Mirrors tasks/SLI_SLO_MATRIX_MEMBER_A.md §2.3–§2.5; change both together.

export const SLO = {
  read: { p95: 300, p99: 1000 },
  write: { p95: 500, p99: 1500 },
  login: { p95: 1000, p99: 2500 },
  refresh: { p95: 300, p99: 1000 },
  // Per-run gate for eligible availability (30-day E2E SLA target is 99.0%, §2.2).
  availability: 0.99,
  serverErrorRate: 0.01,
  workflow: {
    bookingConfirmedP95: 5000,
    checkinTicketP95: 3000,
    examCompletedP95: 5000,
    encounterOpenP95: 5000,
    notificationInboxP95: 10000,
  },
};

// Statuses excluded from the availability denominator (§1.2).
export const NON_ELIGIBLE_STATUSES = [400, 401, 403, 404, 409, 422, 429];

/**
 * Standard API thresholds. Metric names are emitted by lib/api.js.
 * `abort` turns latency/error gates into abort-on-fail (used by breakpoint.js).
 */
export function apiThresholds({ abort = false, includeLogin = true, writeP95 = SLO.write.p95, readP95 = SLO.read.p95 } = {}) {
  const gate = (expr) => (abort ? { threshold: expr, abortOnFail: true, delayAbortEval: '30s' } : expr);
  const thresholds = {
    slo_read_duration: [gate(`p(95)<${readP95}`), `p(99)<${SLO.read.p99}`],
    slo_write_duration: [gate(`p(95)<${writeP95}`), `p(99)<${SLO.write.p99}`],
    slo_server_errors: [gate(`rate<${SLO.serverErrorRate}`)],
    slo_availability: [`rate>=${SLO.availability}`],
  };
  if (includeLogin) {
    thresholds.slo_login_duration = [`p(95)<${SLO.login.p95}`, `p(99)<${SLO.login.p99}`];
  }
  return thresholds;
}

export function workflowThresholds() {
  return {
    workflow_booking_confirmed_duration: [`p(95)<${SLO.workflow.bookingConfirmedP95}`],
    workflow_checkin_ticket_duration: [`p(95)<${SLO.workflow.checkinTicketP95}`],
    workflow_exam_completed_duration: [`p(95)<${SLO.workflow.examCompletedP95}`],
    workflow_encounter_open_duration: [`p(95)<${SLO.workflow.encounterOpenP95}`],
    workflow_notification_inbox_duration: [`p(95)<${SLO.workflow.notificationInboxP95}`],
    workflow_success_rate: ['rate>=0.99'],
  };
}
