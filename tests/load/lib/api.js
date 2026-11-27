// HTTP helpers that classify every response against the SLO model (§1.2, §2.3).
import http from 'k6/http';
import { check } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { BASE_URL, DEFAULT_HEADERS, unwrapResponse } from '../config.js';
import { NON_ELIGIBLE_STATUSES } from './slo.js';

export const metrics = {
  read: new Trend('slo_read_duration', true),
  write: new Trend('slo_write_duration', true),
  login: new Trend('slo_login_duration', true),
  refresh: new Trend('slo_refresh_duration', true),
  availability: new Rate('slo_availability'),
  serverErrors: new Rate('slo_server_errors'),
  throttled: new Rate('slo_throttled'),
  unexpected: new Rate('slo_unexpected_status'),
  conflicts: new Counter('slo_business_conflicts'),
  requests: new Counter('slo_requests'),
};

/**
 * Sends a request and records SLO metrics.
 * @param {string} method HTTP method
 * @param {string} path   path relative to BASE_URL
 * @param {object} opts   { token, body, allowed, cls: 'read'|'write'|'login'|'refresh'|'none', name, key, headers, timeout,
 *                          slo: false to exclude an intentionally-failing request from SLO metrics }
 */
export function call(method, path, opts = {}) {
  const { token, body, allowed = [200, 201, 204], cls = method === 'GET' ? 'read' : 'write', name = path, key, headers = {}, timeout, slo = true } = opts;
  const h = { ...DEFAULT_HEADERS, ...headers, 'X-Correlation-ID': correlationId() };
  if (token) h.Authorization = `Bearer ${token}`;
  if (key) h['Idempotency-Key'] = key;
  const params = { headers: h, tags: { endpoint: name, cls }, responseCallback: http.expectedStatuses({ min: 200, max: 499 }) };
  if (timeout) params.timeout = timeout;

  const payload = body === undefined ? null : JSON.stringify(body);
  const res = http.request(method, `${BASE_URL}${path}`, payload, params);
  if (slo) record(res, allowed, cls, name);
  else check(res, { [`${name} -> expected status`]: (r) => allowed.includes(r.status) });
  return res;
}

/** RFC 4122 v4 UUID. The Gateway only keeps client correlation IDs that are valid GUIDs (§5.1). */
export function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

function correlationId() {
  return uuid();
}

export function record(res, allowed, cls, name) {
  const s = res.status;
  metrics.requests.add(1, { cls });
  if (cls !== 'none' && metrics[cls] && s !== 0) metrics[cls].add(res.timings.duration, { endpoint: name });
  const failure = s === 0 || s >= 500;
  metrics.serverErrors.add(failure, { cls });
  if (failure) metrics.availability.add(false, { cls });
  else if (!NON_ELIGIBLE_STATUSES.includes(s)) metrics.availability.add(true, { cls });
  metrics.throttled.add(s === 429, { cls });
  if (s === 409) metrics.conflicts.add(1, { endpoint: name });
  const expected = allowed.includes(s);
  metrics.unexpected.add(!expected, { cls });
  check(res, { [`${name} -> expected status`]: () => expected });
}

export function json(res) {
  try {
    return unwrapResponse(JSON.parse(res.body));
  } catch {
    return null;
  }
}

export function idOf(res) {
  const v = json(res);
  return v?.id || v?.appointmentId || v?.queueTicketId || null;
}

export const get = (path, opts) => call('GET', path, opts);
export const post = (path, opts) => call('POST', path, opts);
export const patch = (path, opts) => call('PATCH', path, opts);
export const put = (path, opts) => call('PUT', path, opts);
