// Soak: moderate constant offered load for a long window (default 60 min) to expose memory leaks,
// connection-pool exhaustion, outbox growth or latency drift. Compares p95 of the first and last
// 10 minutes; drift > 1.5x is reported as a failure in the summary (soakDriftOk=false).
import { Trend } from 'k6/metrics';
import { apiThresholds } from './lib/slo.js';
import { metrics } from './lib/api.js';
import { runAction, setupMix, teardownMix, THINK, vusFor } from './lib/mix.js';
import { think } from './lib/clinic.js';
import { summarize } from './lib/summary.js';

const RATE = Number(__ENV.SOAK_RATE || 8);
const DURATION_MIN = Number(__ENV.SOAK_MINUTES || 60);
const WINDOW_MIN = Math.min(10, Math.floor(DURATION_MIN / 3));
const MAX_DRIFT = Number(__ENV.SOAK_MAX_DRIFT || 1.5);

const early = new Trend('soak_early_write_duration', true);
const late = new Trend('soak_late_write_duration', true);

export const options = {
  scenarios: {
    // 2-minute ramp spreads session creation (logins) instead of a cold login burst at t=0.
    soak: {
      executor: 'ramping-arrival-rate', startRate: 1, timeUnit: '1s',
      stages: [{ target: RATE, duration: '2m' }, { target: RATE, duration: `${DURATION_MIN}m` }],
      ...vusFor(RATE),
    },
  },
  thresholds: {
    ...apiThresholds(),
    soak_late_write_duration: [`p(95)<500`],
    dropped_iterations: [`count<${RATE * 30}`],
  },
};

export function setup() {
  return { ...setupMix({ minimumSlots: 300 }), startedAt: Date.now() };
}

export default function (data) {
  const before = Date.now();
  runAction(data);
  const took = Date.now() - before;
  think(THINK.min, THINK.max);
  const elapsedMin = (before - data.startedAt) / 60000 - 2; // minutes since the end of the ramp
  if (elapsedMin >= 0 && elapsedMin < WINDOW_MIN) early.add(took);
  else if (elapsedMin > DURATION_MIN - WINDOW_MIN) late.add(took);
}

export function teardown(data) {
  teardownMix(data);
}

export function handleSummary(data) {
  void metrics;
  const e = data.metrics.soak_early_write_duration?.values?.['p(95)'];
  const l = data.metrics.soak_late_write_duration?.values?.['p(95)'];
  const drift = e && l ? l / e : null;
  return summarize('soak', data, {
    rate: RATE, minutes: DURATION_MIN, windowMinutes: WINDOW_MIN,
    actionP95EarlyMs: e ?? null, actionP95LateMs: l ?? null, drift, soakDriftOk: drift === null ? null : drift <= MAX_DRIFT,
  });
}
