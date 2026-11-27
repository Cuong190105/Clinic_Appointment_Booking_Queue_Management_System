// Capacity / breakpoint test (open model): raise the offered rate step by step until the
// write p95, read p95 or 5xx gate fails, then abort. The summary reports the last step that held,
// which converts to supported users as: users ≈ actions/s × average think time per action.
import { apiThresholds } from './lib/slo.js';
import { act, setupMix, teardownMix, vusFor } from './lib/mix.js';
import { summarize } from './lib/summary.js';

const START = Number(__ENV.BREAK_START_RATE || 5);
const STEP = Number(__ENV.BREAK_STEP_RATE || 5);
const MAX = Number(__ENV.BREAK_MAX_RATE || 120);
const STEP_DURATION_S = Number(__ENV.BREAK_STEP_SECONDS || 90);
const RAMP_S = 15;

const stages = [];
for (let rate = START; rate <= MAX; rate += STEP) {
  stages.push({ target: rate, duration: `${RAMP_S}s` });
  stages.push({ target: rate, duration: `${STEP_DURATION_S}s` });
}

export const options = {
  scenarios: {
    breakpoint: {
      executor: 'ramping-arrival-rate', startRate: START, timeUnit: '1s', stages,
      // Pre-allocate for the top step: creating VUs mid-ramp drops iterations and under-delivers load.
      preAllocatedVUs: vusFor(MAX).preAllocatedVUs, maxVUs: vusFor(MAX).maxVUs,
    },
  },
  thresholds: apiThresholds({ abort: true }),
};

export function setup() {
  return { ...setupMix({ minimumSlots: Math.max(300, MAX * 10) }), startedAt: Date.now() };
}

export default function (data) {
  act(data);
}

export function teardown(data) {
  teardownMix(data);
}

export function handleSummary(data) {
  const elapsedS = (data.state?.testRunDurationMs || 0) / 1000;
  const stepSpan = RAMP_S + STEP_DURATION_S;
  const stepIndex = Math.floor(elapsedS / stepSpan);
  const aborted = Object.values(data.metrics || {}).some((m) => m.thresholds && Object.values(m.thresholds).some((t) => !t.ok));
  const reachedRate = Math.min(MAX, START + STEP * stepIndex);
  // Abort evaluation is delayed 30s, so the failing step is the one that was running; the last sustained step is one below.
  const lastSustainedRate = aborted ? Math.max(0, reachedRate - STEP) : reachedRate;
  return summarize('breakpoint', data, {
    stepPlan: { start: START, step: STEP, max: MAX, stepSeconds: STEP_DURATION_S },
    aborted,
    rateAtStop: reachedRate,
    lastSustainedRatePerSecond: lastSustainedRate,
  });
}
