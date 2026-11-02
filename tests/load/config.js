// Shared configuration and SLO thresholds for Clinic Microservices Load Tests
export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

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
