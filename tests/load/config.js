// Shared configuration and SLO thresholds for Clinic Microservices Load Tests (Phase 9)
import http from 'k6/http';

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const SLO_THRESHOLDS = {
  // Phase 9 SLO: HTTP errors <= 1% (excluding 409 contention and 429 rate-limit)
  http_req_failed: ['rate<0.01'],
  // Phase 9 SLO: p95 latency <= 500ms, p99 <= 1500ms
  http_req_duration: ['p(95)<500', 'p(99)<1500'],
};

export const DEFAULT_HEADERS = {
  'Content-Type': 'application/json',
  'Accept': 'application/json',
  'X-Correlation-ID': 'load-test-runner',
};

// Cache tokens in memory per VU during runtime
let cachedTokens = null;

export function getCachedTokens() {
  return cachedTokens;
}

/**
 * Log in to Identity Service via API Gateway and obtain a real JWT Bearer token.
 * Requires SEED_PASSWORD via __ENV.
 */
export function login(username, password = __ENV.SEED_PASSWORD) {
  if (!password) {
    throw new Error('Missing mandatory environment variable SEED_PASSWORD');
  }
  if (cachedTokens) {
    if (username.startsWith('patient') && cachedTokens.patients) {
      const match = cachedTokens.patients.find((p) => p.username === username);
      if (match && match.token) return match.token;
      if (cachedTokens.patients.length > 0) return cachedTokens.patients[0].token;
    }
    if (username.startsWith('doctor') && cachedTokens.doctors && cachedTokens.doctors.length > 0) {
      return cachedTokens.doctors[0].token;
    }
    if (username.startsWith('receptionist') && cachedTokens.receptionists && cachedTokens.receptionists.length > 0) {
      return cachedTokens.receptionists[0].token;
    }
    if (username.startsWith('admin') && cachedTokens.admins && cachedTokens.admins.length > 0) {
      return cachedTokens.admins[0].token;
    }
  }

  const payload = JSON.stringify({ username, password });
  const res = http.post(`${BASE_URL}/auth/login`, payload, {
    headers: DEFAULT_HEADERS,
  });

  if (res.status === 200) {
    try {
      const data = JSON.parse(res.body);
      return data.result?.accessToken || null;
    } catch (e) {
      return null;
    }
  }
  return null;
}

/**
 * Generate authenticated headers with optional idempotency and correlation tracking
 */
export function getAuthHeaders(token, idempotencyKey = null, correlationId = null) {
  const headers = {
    ...DEFAULT_HEADERS,
    'Authorization': `Bearer ${token}`,
  };
  if (idempotencyKey) {
    headers['Idempotency-Key'] = idempotencyKey;
  }
  if (correlationId) {
    headers['X-Correlation-ID'] = correlationId;
  }
  return headers;
}

