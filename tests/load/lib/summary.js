// Compact, sanitized summary (no setup_data / tokens) with threshold verdicts per metric.
const ENV = __ENV;
const KEYS = ['count', 'rate', 'value', 'avg', 'med', 'min', 'max', 'p(90)', 'p(95)', 'p(99)', 'passes', 'fails'];

export function summarize(profile, data, extra = {}) {
  const metrics = {};
  let thresholdsPassed = true;
  for (const [name, metric] of Object.entries(data.metrics || {})) {
    if (!/^(slo_|workflow_|booking_|qos_|fault_|soak_|contention_|http_req_failed|http_req_duration$|iterations|dropped_iterations|vus_max)/.test(name)) continue;
    const values = metric.values || {};
    const picked = {};
    for (const key of KEYS) if (values[key] !== undefined) picked[key] = values[key];
    if (metric.thresholds) {
      picked.thresholds = Object.fromEntries(Object.entries(metric.thresholds).map(([expr, r]) => [expr, r.ok]));
      if (Object.values(metric.thresholds).some((r) => !r.ok)) thresholdsPassed = false;
    }
    metrics[name] = picked;
  }
  // Collect failing checks (name + counts) so a red run is diagnosable without the default k6 summary.
  const failedChecks = [];
  const walk = (group) => {
    for (const c of Object.values(group?.checks || {})) {
      if (c.fails > 0) failedChecks.push({ name: c.name, passes: c.passes, fails: c.fails });
    }
    for (const g of Object.values(group?.groups || {})) walk(g);
  };
  walk(data.root_group);
  const summary = {
    profile,
    baseUrl: ENV.BASE_URL || 'http://localhost:5000',
    target: ENV.TARGET_SYSTEM || 'unspecified',
    runAt: new Date().toISOString(),
    testRunDurationMs: data.state?.testRunDurationMs,
    thresholdsPassed,
    failedChecks,
    ...extra,
    metrics,
  };
  const text = `${JSON.stringify(summary, null, 2)}\n`;
  const out = { stdout: text };
  const file = ENV.SUMMARY_EXPORT || (ENV.RESULTS_DIR ? `${ENV.RESULTS_DIR}/${profile}-summary.json` : null);
  if (file) out[file] = text;
  return out;
}
