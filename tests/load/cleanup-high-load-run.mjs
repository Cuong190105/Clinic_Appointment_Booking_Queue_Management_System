// Cancel only upcoming appointments created by one booking-high-load.js run.
// Pass HIGH_LOAD_RUN_ID used for the run; no credentials or patient data are printed.
const baseUrl = process.env.BASE_URL || 'http://localhost:5100';
const username = process.env.BENCHMARK_PATIENT_USERNAME || 'patient1';
const password = process.env.BENCHMARK_PATIENT_PASSWORD || 'Password123!';
const runId = process.env.HIGH_LOAD_RUN_ID;
const batchSize = Number(process.env.CLEANUP_CONCURRENCY || 20);

if (!runId) throw new Error('HIGH_LOAD_RUN_ID is required.');

function unwrap(value) {
  for (let i = 0; i < 3; i++) {
    if (Array.isArray(value)) return value;
    if (value?.result !== undefined) value = value.result;
    else if (value?.data !== undefined) value = value.data;
    else break;
  }
  return value;
}

async function request(path, token, init = {}) {
  return fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init.headers || {}),
    },
  });
}

const login = await request('/auth/login', undefined, {
  method: 'POST',
  body: JSON.stringify({ username, password }),
});
if (login.status !== 200) throw new Error(`Patient login failed: HTTP ${login.status}.`);
const auth = unwrap(await login.json());
const token = auth?.accessToken || auth?.token;
if (!token) throw new Error('Login response did not contain an access token.');

const listResponse = await request('/me/appointments?category=upcoming', token);
if (listResponse.status !== 200) throw new Error(`Upcoming appointment query failed: HTTP ${listResponse.status}.`);
const appointments = unwrap(await listResponse.json());
if (!Array.isArray(appointments)) throw new Error('Upcoming appointment response was not a list.');

const marker = `High Load Stress ${runId}`;
const targets = appointments.filter((item) =>
  String(item.reason || '').includes(marker) && String(item.status).toLowerCase() !== 'cancelled'
);
let cancelled = 0;
let failed = 0;

for (let offset = 0; offset < targets.length; offset += batchSize) {
  const batch = targets.slice(offset, offset + batchSize);
  const results = await Promise.all(batch.map(async (appointment) => {
    const response = await request(`/appointments/${appointment.id}/cancel`, token, { method: 'POST' });
    return response.status === 200 || response.status === 204;
  }));
  cancelled += results.filter(Boolean).length;
  failed += results.filter((ok) => !ok).length;
}

const verifyResponse = await request('/me/appointments?category=upcoming', token);
if (verifyResponse.status !== 200) throw new Error(`Cleanup verification query failed: HTTP ${verifyResponse.status}.`);
const remaining = unwrap(await verifyResponse.json()).filter((item) =>
  String(item.reason || '').includes(marker) && String(item.status).toLowerCase() !== 'cancelled'
);

console.log(JSON.stringify({ runId, found: targets.length, cancelAccepted: cancelled, cancelFailed: failed, remainingActive: remaining.length }));
if (failed || remaining.length) process.exitCode = 1;
