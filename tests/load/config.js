// Shared configuration for k6 and Node benchmark scripts.
const ENV = typeof __ENV !== 'undefined' ? __ENV : typeof process !== 'undefined' ? process.env : {};

export const BASE_URL = ENV.BASE_URL || 'http://localhost:5000';
export const CLINIC_UTC_OFFSET_MINUTES = Number(ENV.CLINIC_UTC_OFFSET_MINUTES || 420);
export const DOCTOR_ID = ENV.BENCHMARK_DOCTOR_ID || 'a0000004-0000-0000-0000-000000000001';
export const ADMIN_USERNAME = ENV.BENCHMARK_ADMIN_USERNAME || 'admin1';
export const ADMIN_PASSWORD = ENV.BENCHMARK_ADMIN_PASSWORD || 'Password123!';
export const PATIENT_USERNAME = ENV.BENCHMARK_PATIENT_USERNAME || 'patient1';
export const PATIENT_PASSWORD = ENV.BENCHMARK_PATIENT_PASSWORD || 'Password123!';
export const RECEPTIONIST_USERNAME = ENV.BENCHMARK_RECEPTIONIST_USERNAME || 'receptionist1';
export const RECEPTIONIST_PASSWORD = ENV.BENCHMARK_RECEPTIONIST_PASSWORD || 'Password123!';

export const SLO_THRESHOLDS = {
  // Phase 8 SLO: HTTP errors <= 1%
  http_req_failed: ['rate<0.01'],
  // Phase 8 SLO: p95 latency <= 500ms, p99 <= 1500ms
  http_req_duration: ['p(95)<500', 'p(99)<1500'],
};

export const DEFAULT_HEADERS = {
  'Content-Type': 'application/json',
  'Accept': 'application/json',
  'X-Correlation-ID': 'load-test-runner',
};

export function getAuthHeaders(token) {
  return {
    ...DEFAULT_HEADERS,
    'Authorization': `Bearer ${token}`,
  };
}

export function clinicDate(daysFromToday = 0) {
  const clinicNow = new Date(Date.now() + CLINIC_UTC_OFFSET_MINUTES * 60_000);
  clinicNow.setUTCHours(0, 0, 0, 0);
  clinicNow.setUTCDate(clinicNow.getUTCDate() + daysFromToday);
  return clinicNow.toISOString().slice(0, 10);
}

export function unwrapResponse(body) {
  let value = body;
  for (let i = 0; i < 3; i++) {
    if (Array.isArray(value)) return value;
    if (value?.result !== undefined) value = value.result;
    else if (value?.data !== undefined) value = value.data;
    else break;
  }
  return value;
}

export function responseItems(body, keys = ['items', 'schedules']) {
  const value = unwrapResponse(body);
  if (Array.isArray(value)) return value;
  for (const key of keys) {
    if (Array.isArray(value?.[key])) return value[key];
  }
  return [];
}
