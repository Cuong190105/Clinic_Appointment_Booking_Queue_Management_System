// Equal-offered-load comparison (open model). Both systems receive exactly LOAD_RATE user actions/s,
// so latency differences are not distorted by closed-loop feedback (coordinated omission).
// Run at several rates, e.g. LOAD_RATE=10, 25, 50, on each stack sequentially.
//
// One scenario ramps to LOAD_RATE during LOAD_WARMUP so user sessions (logins) are created gradually,
// as in a real clinic morning; only the steady phase after the ramp is gated. (k6 scenarios do not
// share VUs, so a separate warm-up scenario would make every steady-phase VU log in at once.)
import exec from 'k6/execution';
import { apiThresholds } from './lib/slo.js';
import { act, setupMix, teardownMix, vusFor } from './lib/mix.js';
import { summarize } from './lib/summary.js';

const RATE = Number(__ENV.LOAD_RATE || 10);
const DURATION = __ENV.LOAD_DURATION || '5m';
const WARMUP = __ENV.LOAD_WARMUP || '60s';
const toSeconds = (d) => Number(String(d).replace(/[a-z]+$/i, '')) * ({ s: 1, m: 60, h: 3600 }[String(d).match(/[a-z]+$/i)?.[0] || 's']);
const WARMUP_S = toSeconds(WARMUP);

export const options = {
  scenarios: {
    load: {
      executor: 'ramping-arrival-rate', startRate: 1, timeUnit: '1s',
      stages: [{ target: RATE, duration: WARMUP }, { target: RATE, duration: DURATION }],
      ...vusFor(RATE),
    },
  },
  thresholds: Object.fromEntries(Object.entries(apiThresholds()).map(([m, t]) => [`${m}{phase:steady}`, t]).concat([
    // The generator must actually deliver the offered load; dropped iterations mean the result is invalid.
    ['dropped_iterations', [`count<${Math.ceil(RATE * 5)}`]],
  ])),
};

export function setup() {
  // Bookings are cancelled after confirmation, so slots are recycled; still require headroom.
  return { ...setupMix({ minimumSlots: Math.max(200, RATE * 20) }), startedAt: Date.now() };
}

export default function (data) {
  exec.vu.tags.phase = (Date.now() - data.startedAt) / 1000 < WARMUP_S ? 'warmup' : 'steady';
  act(data);
}

export function teardown(data) {
  teardownMix(data);
}

export function handleSummary(data) {
  return summarize('load', data, { offeredRatePerSecond: RATE, warmup: WARMUP, duration: DURATION });
}
