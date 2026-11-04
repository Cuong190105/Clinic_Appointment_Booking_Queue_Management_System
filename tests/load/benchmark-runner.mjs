#!/usr/bin/env node
/**
 * Standalone Cross-Platform Microservices Load & Benchmark Runner (Node.js) - Phase 9
 * Executes reproducible concurrency workloads covering both:
 * 1. Read Baseline (Specialties, Doctors, My Appointments)
 * 2. Business Write Baseline (Online SAGA Appointment Booking with Idempotency & Outbox)
 *
 * Computes separate throughput, p50, p90, p95, p99 percentiles for both overall,
 * successful writes (201 Created), and contention/conflict (409 Conflict).
 * Enforces strict Phase 9 SLO compliance including mandatory successCount > 0.
 */

import fs from 'fs';
import path from 'path';

const BASE_URL = process.env.BASE_URL || 'http://localhost:5000';
const CONCURRENCY = parseInt(process.env.CONCURRENCY || '10', 10);
const TOTAL_REQUESTS = parseInt(process.env.TOTAL_REQUESTS || '100', 10);
const WRITE_CONCURRENCY = parseInt(process.env.WRITE_CONCURRENCY || '2', 10);
const WRITE_REQUESTS = parseInt(process.env.WRITE_REQUESTS || '50', 10);
const OUTPUT_FILE = process.env.BENCHMARK_OUTPUT || 'tests/load/evidence/raw_baseline.json';
const TEST_RUN_ID = process.env.TEST_RUN_ID || 'PH9-BENCH';

console.log('='.repeat(75));
console.log(`CLINIC MICROSERVICES DUAL-WORKLOAD BENCHMARK RUNNER (Phase 9)`);
console.log(`Target Gateway     : ${BASE_URL}`);
console.log(`Read Concurrency   : ${CONCURRENCY} VUs (${TOTAL_REQUESTS} requests)`);
console.log(`Write Concurrency  : ${WRITE_CONCURRENCY} VUs (${WRITE_REQUESTS} requests)`);
console.log(`Output Evidence    : ${OUTPUT_FILE}`);
console.log(`TestRunId Tag      : [${TEST_RUN_ID}]`);
console.log('='.repeat(75));

function calculatePercentiles(latencies) {
  if (latencies.length === 0) {
    return { min: 0, p50: 0, p90: 0, p95: 0, p99: 0, max: 0, avg: 0 };
  }
  const sorted = [...latencies].sort((a, b) => a - b);
  const p = (pct) => sorted[Math.min(sorted.length - 1, Math.floor((pct / 100) * sorted.length))];
  return {
    min: Number(sorted[0].toFixed(2)),
    p50: Number(p(50).toFixed(2)),
    p90: Number(p(90).toFixed(2)),
    p95: Number(p(95).toFixed(2)),
    p99: Number(p(99).toFixed(2)),
    max: Number(sorted[sorted.length - 1].toFixed(2)),
    avg: Number((sorted.reduce((acc, v) => acc + v, 0) / sorted.length).toFixed(2)),
  };
}

async function getAuthTokens() {
  const seedPassword = process.env.SEED_PASSWORD;
  if (!seedPassword) {
    throw new Error('Missing mandatory environment variable SEED_PASSWORD');
  }

  // Check out-of-tree cached tokens if present
  const tempTokenPath = path.join(process.env.TEMP || '', 'clinic_phase9_tokens.json');
  if (fs.existsSync(tempTokenPath)) {
    try {
      const cached = JSON.parse(fs.readFileSync(tempTokenPath, 'utf8'));
      if (cached.patients && cached.patients.length > 0) {
        console.log(`[Auth] Loaded ${cached.patients.length} pre-generated tokens from ${tempTokenPath}`);
        return cached.patients.map((p) => p.token);
      }
    } catch (e) {
      // Continue to in-memory login
    }
  }

  console.log('[Auth] Authenticating patient accounts via API Gateway /auth/login...');
  const tokens = [];
  for (let i = 1; i <= Math.min(15, CONCURRENCY); i++) {
    try {
      const res = await fetch(`${BASE_URL}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: `patient${i}`, password: seedPassword }),
      });
      if (res.ok) {
        const data = await res.json();
        const tok = data.result?.accessToken;
        if (tok) tokens.push(tok);
      }
    } catch (e) {
      // Ignore login error and continue
    }
  }
  console.log(`[Auth] Successfully obtained ${tokens.length} active bearer tokens in memory.\n`);
  return tokens;
}

async function runWorkloadPass(name, endpointsGenerator, tokens, totalRequests, concurrency, isWritePass = false) {
  console.log(`>>> Starting Workload Pass: ${name} (${totalRequests} requests @ Concurrency ${concurrency})`);
  const latencies = [];
  const successLatencies = [];
  const conflictLatencies = [];
  const statusCodes = {};
  let server5xxCount = 0;
  let client4xxCount = 0;
  let successCount = 0;

  const queue = Array.from({ length: totalRequests }, (_, idx) => idx);
  const startTime = Date.now();

  async function worker(workerId) {
    const token = tokens[(workerId - 1) % tokens.length];

    while (queue.length > 0) {
      const reqIndex = queue.shift();
      if (reqIndex === undefined) break;

      const reqDef = endpointsGenerator(reqIndex, workerId, token);
      const reqStart = performance.now();

      try {
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 10000);

        const response = await fetch(reqDef.url, {
          method: reqDef.method || 'GET',
          headers: reqDef.headers,
          body: reqDef.body || undefined,
          signal: controller.signal,
        });

        clearTimeout(timeout);
        const reqEnd = performance.now();
        const duration = reqEnd - reqStart;
        latencies.push(duration);

        const status = response.status;
        statusCodes[status] = (statusCodes[status] || 0) + 1;

        if (status >= 200 && status < 300) {
          successCount++;
          successLatencies.push(duration);
        } else if (status === 409 || status === 429) {
          if (status === 409) conflictLatencies.push(duration);
        } else if (status >= 400 && status < 500) {
          client4xxCount++;
        } else if (status >= 500) {
          server5xxCount++;
        }
      } catch (err) {
        const reqEnd = performance.now();
        latencies.push(reqEnd - reqStart);
        server5xxCount++;
        statusCodes['599_TIMEOUT'] = (statusCodes['599_TIMEOUT'] || 0) + 1;
      }

      await new Promise((r) => setTimeout(r, isWritePass ? 60 : 5));
    }
  }

  const workers = Array.from({ length: concurrency }, (_, idx) => worker(idx + 1));
  await Promise.all(workers);

  const totalTimeSec = (Date.now() - startTime) / 1000;
  const throughput = Number((latencies.length / totalTimeSec).toFixed(2));
  const percentiles = calculatePercentiles(latencies);
  const successPercentiles = successLatencies.length > 0 ? calculatePercentiles(successLatencies) : null;
  const conflictPercentiles = conflictLatencies.length > 0 ? calculatePercentiles(conflictLatencies) : null;

  console.log(`<<< Completed Pass: ${name} in ${totalTimeSec.toFixed(2)}s | Throughput: ${throughput} RPS`);
  console.log(`    Status Breakdown: ${JSON.stringify(statusCodes)}`);
  console.log(`    Overall Latencies (ms) : min=${percentiles.min}, p50=${percentiles.p50}, p95=${percentiles.p95}, p99=${percentiles.p99}, max=${percentiles.max}`);
  if (successPercentiles) {
    console.log(`    Success (2xx) Latency  : count=${successCount}, min=${successPercentiles.min}, p50=${successPercentiles.p50}, p95=${successPercentiles.p95}, p99=${successPercentiles.p99}`);
  }
  if (conflictPercentiles) {
    console.log(`    Conflict (409) Latency : count=${conflictLatencies.length}, min=${conflictPercentiles.min}, p50=${conflictPercentiles.p50}, p95=${conflictPercentiles.p95}`);
  }
  console.log('');

  const targetP95 = isWritePass ? parseInt(process.env.WRITE_SLO_P95 || '1500', 10) : 500;
  const targetP99 = isWritePass ? parseInt(process.env.WRITE_SLO_P99 || '2500', 10) : 1500;
  const evaluatedP95 = (isWritePass && successPercentiles) ? successPercentiles.p95 : percentiles.p95;
  const evaluatedP99 = (isWritePass && successPercentiles) ? successPercentiles.p99 : percentiles.p99;

  const isSloMet = (!isWritePass || successCount > 0) &&
    evaluatedP95 <= targetP95 &&
    evaluatedP99 <= targetP99 &&
    (server5xxCount / latencies.length) <= 0.01;

  return {
    name,
    totalRequests: latencies.length,
    durationSeconds: totalTimeSec,
    throughputRps: throughput,
    percentiles,
    successPercentiles,
    conflictPercentiles,
    statusCodes,
    server5xxCount,
    client4xxCount,
    successCount,
    conflictCount: conflictLatencies.length,
    isSloMet,
  };
}

async function main() {
  const tokens = await getAuthTokens();
  if (tokens.length === 0) {
    console.error('CRITICAL: No authentication tokens available for benchmark!');
    process.exit(1);
  }

  // --- PASS 1: Read Baseline Workload ---
  const readEndpoints = [
    (idx, wId, tok) => ({
      url: `${BASE_URL}/specialties`,
      method: 'GET',
      headers: {
        'Accept': 'application/json',
        'Authorization': `Bearer ${tok}`,
        'X-Correlation-ID': `bench-read-spec-${wId}-${idx}`,
      },
    }),
    (idx, wId, tok) => ({
      url: `${BASE_URL}/doctors`,
      method: 'GET',
      headers: {
        'Accept': 'application/json',
        'Authorization': `Bearer ${tok}`,
        'X-Correlation-ID': `bench-read-doc-${wId}-${idx}`,
      },
    }),
    (idx, wId, tok) => ({
      url: `${BASE_URL}/me/appointments`,
      method: 'GET',
      headers: {
        'Accept': 'application/json',
        'Authorization': `Bearer ${tok}`,
        'X-Correlation-ID': `bench-read-me-${wId}-${idx}`,
      },
    }),
  ];

  const readPassResult = await runWorkloadPass(
    'Read Baseline (Specialties, Doctors, History)',
    (idx, wId, tok) => readEndpoints[idx % readEndpoints.length](idx, wId, tok),
    tokens,
    TOTAL_REQUESTS,
    CONCURRENCY,
    false
  );

  // --- PASS 2: Business Write Baseline Workload (Online SAGA Booking) ---
  let scheduleId = process.env.WORKLOAD_SCHEDULE_ID;
  if (!scheduleId) {
    try {
      const tempConfigPath = path.join(process.env.TEMP || '', 'clinic_phase9_env.json');
      if (fs.existsSync(tempConfigPath)) {
        const conf = JSON.parse(fs.readFileSync(tempConfigPath, 'utf8'));
        scheduleId = conf.workloadScheduleId;
      }
    } catch (e) {}
  }
  if (!scheduleId) {
    throw new Error('Missing WORKLOAD_SCHEDULE_ID environment variable or temp config');
  }

  console.log(`[Config] Using Workload Schedule ID: ${scheduleId}`);

  const writePassResult = await runWorkloadPass(
    'Business Write Baseline (Online SAGA Booking POST /appointments)',
    (idx, wId, tok) => {
      // 48 distinct slots: 08:00 to 19:45
      const slotIdx = idx % 48;
      const hour = 8 + Math.floor(slotIdx / 4);
      const minute = (slotIdx % 4) * 15;
      const timeSlotStr = `${hour < 10 ? '0' + hour : hour}:${minute < 10 ? '0' + minute : minute}:00`;
      return {
        url: `${BASE_URL}/appointments`,
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json',
          'Authorization': `Bearer ${tok}`,
          'Idempotency-Key': `${TEST_RUN_ID}-w${wId}-r${idx}-${Date.now()}`,
          'X-Correlation-ID': `bench-write-${wId}-${idx}`,
        },
        body: JSON.stringify({
          workScheduleId: scheduleId,
          timeSlot: timeSlotStr,
          reason: `[${TEST_RUN_ID}] Baseline write booking worker ${wId} req ${idx}`,
          isWalkIn: false,
        }),
      };
    },
    tokens,
    WRITE_REQUESTS,
    WRITE_CONCURRENCY,
    true // isWritePass
  );

  // --- Summary & Evidence Output ---
  const overallSummary = {
    metadata: {
      timestamp: new Date().toISOString(),
      baseUrl: BASE_URL,
      concurrency: CONCURRENCY,
      requestsPerPass: TOTAL_REQUESTS,
      testRunId: TEST_RUN_ID,
      workScheduleId: scheduleId,
      nodeVersion: process.version,
    },
    passes: {
      readBaseline: readPassResult,
      writeBaseline: writePassResult,
    },
    combinedSlo: {
      availabilityTarget: '>= 99%',
      readLatencyP95Target: '<= 500ms',
      writeLatencyP95Target: '<= 1500ms (Distributed SAGA)',
      serverError5xxTarget: '<= 1%',
      readPassed: readPassResult.isSloMet,
      writePassed: writePassResult.isSloMet,
      allPassed: readPassResult.isSloMet && writePassResult.isSloMet && writePassResult.successCount > 0,
    },
  };

  const outputDir = path.dirname(OUTPUT_FILE);
  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
  }
  fs.writeFileSync(OUTPUT_FILE, JSON.stringify(overallSummary, null, 2));
  console.log(`[Evidence] Full raw baseline telemetry saved to: ${OUTPUT_FILE}`);

  console.log('\n' + '='.repeat(75));
  console.log('PHASE 9 DUAL BASELINE ACCEPTANCE GATES:');
  console.log(`  Read Baseline         : Throughput=${readPassResult.throughputRps} RPS | p95=${readPassResult.percentiles.p95}ms | 5xx=${readPassResult.server5xxCount} -> ${readPassResult.isSloMet ? 'PASSED' : 'FAILED'}`);
  console.log(`  Write Baseline Overall: Throughput=${writePassResult.throughputRps} RPS | p95=${writePassResult.percentiles.p95}ms | 5xx=${writePassResult.server5xxCount} -> ${writePassResult.isSloMet ? 'PASSED' : 'FAILED'}`);
  console.log(`  - Successful Writes   : Count=${writePassResult.successCount} (Target: >0) | p50=${writePassResult.successPercentiles?.p50}ms | p95=${writePassResult.successPercentiles?.p95}ms`);
  console.log(`  - Business Conflicts  : Count=${writePassResult.conflictCount} | p50=${writePassResult.conflictPercentiles?.p50}ms | p95=${writePassResult.conflictPercentiles?.p95}ms`);
  console.log('='.repeat(75));

  if (!overallSummary.combinedSlo.allPassed) {
    console.error('\n[SLO VIOLATION] Baseline benchmarks failed to meet required thresholds (or successCount was 0).');
    process.exit(1);
  }

  console.log('\n[SUCCESS] Both Read and Business Write Baseline passes meet 100% of Phase 9 SLOs.');
  process.exit(0);
}

main().catch((err) => {
  console.error('Fatal error during benchmark execution:', err);
  process.exit(1);
});
