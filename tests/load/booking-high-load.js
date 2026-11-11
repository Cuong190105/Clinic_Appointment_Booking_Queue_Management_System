import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import {
  BASE_URL,
  DEFAULT_HEADERS,
  PATIENT_PASSWORD,
  PATIENT_USERNAME,
  clinicDate,
  parseAppointment,
  responseItems,
  unwrapResponse,
} from './config.js';
import { adminToken, cancelWorkSchedule, login } from './fixtures.js';

// Configuration: Overridable via environment variables
// e.g. k6 run -e HIGH_LOAD_VUS=30 -e TEST_DURATION=60s tests/load/booking-high-load.js
const HIGH_LOAD_VUS = Number(__ENV.HIGH_LOAD_VUS || 30);
const TEST_DURATION = __ENV.TEST_DURATION || '60s';
const RAMP_DURATION = __ENV.RAMP_DURATION || '10s';

// Custom Metrics for High Load Write Benchmark
const bookingSuccessRate = new Rate('booking_success_rate');
const serverErrorRate = new Rate('server_error_rate');
const confirmationRate = new Rate('booking_confirmed_rate');
const idempotencyRate = new Rate('booking_idempotency_verified_rate');

const bookingApiDuration = new Trend('booking_api_duration', true);
const bookingCreatedApiDuration = new Trend('booking_created_api_duration', true);
const bookingConflictApiDuration = new Trend('booking_conflict_api_duration', true);
const bookingE2eDuration = new Trend('booking_e2e_duration', true);

const totalBookingsAttempted = new Counter('total_bookings_attempted');
const totalBookingsCreated = new Counter('total_bookings_created');
const totalBookingsConfirmed = new Counter('total_bookings_confirmed');
const totalConflictsEncountered = new Counter('total_conflicts_encountered');

export const options = {
  scenarios: {
    sustained_high_load_booking: {
      executor: 'ramping-vus',
      startVUs: 5,
      stages: [
        { duration: RAMP_DURATION, target: HIGH_LOAD_VUS }, // Ramp-up
        { duration: TEST_DURATION, target: HIGH_LOAD_VUS }, // Sustained High Load (~15k requests)
        { duration: '10s', target: 0 },                     // Ramp-down
      ],
      gracefulRampDown: '15s',
    },
  },
  thresholds: {
    // 95% of all API responses within 500ms, 99% within 1500ms
    booking_api_duration: ['p(95)<500', 'p(99)<1500'],
    // 95% of successful write transactions within 1000ms
    booking_created_api_duration: ['p(95)<1000'],
    // 95% of E2E SAGA confirmations within 5000ms
    booking_e2e_duration: ['p(95)<5000'],
    // Over 98% of requests return valid business response (201 Created or 409 Conflict)
    booking_success_rate: ['rate>0.98'],
    // Over 90% of created bookings successfully reach Confirmed
    booking_confirmed_rate: ['rate>0.90'],
    // Zero or negligible server crashes/unhandled 5xx errors (< 1%)
    server_error_rate: ['rate<0.01'],
  },
};

function appointmentIdFrom(body) {
  const appointment = parseAppointment(body);
  return appointment?.id || appointment?.appointmentId || null;
}

function isConfirmed(body) {
  const appointment = parseAppointment(body);
  return appointment?.status === 'Confirmed' || appointment?.status === 1;
}

// 10 Doctors available in seed data
const DOCTOR_IDS = [
  'a0000004-0000-0000-0000-000000000001',
  'a0000004-0000-0000-0000-000000000002',
  'a0000004-0000-0000-0000-000000000003',
  'a0000004-0000-0000-0000-000000000004',
  'a0000004-0000-0000-0000-000000000005',
  'a0000004-0000-0000-0000-000000000006',
  'a0000004-0000-0000-0000-000000000007',
  'a0000004-0000-0000-0000-000000000008',
  'a0000004-0000-0000-0000-000000000009',
  'a0000004-0000-0000-0000-000000000010',
];

// 5 target dates in the future: Days +3 through +7
// 10 doctors x 5 dates = 50 shifts x 48 slots = 2,400 slots!
const TARGET_DAY_OFFSETS = [3, 4, 5, 6, 7];

export function setup() {
  const admin = adminToken();
  const patientToken = login(PATIENT_USERNAME, PATIENT_PASSWORD);
  const runId = `hl2k-${Date.now()}-${Math.floor(Math.random() * 100000)}`;

  console.log(`[SETUP] Initializing 2,400 Slot Fixtures (10 Doctors x 5 Days) for runId: ${runId}`);

  // Warm-up: Ensure JIT & Model loaded
  http.get(`${BASE_URL}/specialties`, { headers: DEFAULT_HEADERS });

  const schedules = [];
  const allSlots = [];

  for (const dayOffset of TARGET_DAY_OFFSETS) {
    const targetDate = clinicDate(dayOffset);

    for (let i = 0; i < DOCTOR_IDS.length; i++) {
      const docId = DOCTOR_IDS[i];
      let scheduleId = null;
      let createdByTest = false;

      try {
        const res = http.post(
          `${BASE_URL}/doctors/${docId}/shifts`,
          JSON.stringify({
            date: targetDate,
            startTime: '08:00:00',
            endTime: '20:00:00',
            recommendedCapacity: 100,
          }),
          {
            headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${admin}` },
            tags: { operation: 'setup-create-shift' },
          }
        );

        if (res.status === 200 || res.status === 201) {
          const body = unwrapResponse(JSON.parse(res.body));
          if (body?.id) {
            scheduleId = body.id;
            createdByTest = true;
          }
        } else {
          // Query existing active shift if already present
          const listRes = http.get(
            `${BASE_URL}/doctors/${docId}/shifts?startDate=${targetDate}&endDate=${targetDate}`,
            { headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${admin}` } }
          );
          if (listRes.status === 200) {
            const list = responseItems(JSON.parse(listRes.body));
            const active = list.find((s) => s.status === 'Active' || s.status === 0);
            if (active?.id) {
              scheduleId = active.id;
              createdByTest = false;
            }
          }
        }
      } catch (e) {
        console.warn(`Could not provision shift for doctor ${docId} on ${targetDate}: ${e.message}`);
      }

      if (scheduleId) {
        schedules.push({ id: scheduleId, doctorId: docId, date: targetDate, createdByTest });

        // Query available slots
        const availRes = http.get(`${BASE_URL}/doctors/${docId}/available?date=${targetDate}`, {
          headers: DEFAULT_HEADERS,
        });
        if (availRes.status === 200) {
          const parsed = JSON.parse(availRes.body);
          const items = responseItems(parsed, ['timeSlot', 'timeSlots']);
          const found = items.find((item) => item.id === scheduleId);
          const slots = Array.isArray(found?.timeSlot)
            ? found.timeSlot
            : Array.isArray(found?.timeSlots)
            ? found.timeSlots
            : [];

          for (const s of slots) {
            allSlots.push({
              scheduleId,
              doctorId: docId,
              date: targetDate,
              timeSlot: s,
            });
          }
        }
      }
    }
  }

  console.log(`[SETUP] Successfully provisioned ${schedules.length} doctor work schedules with ${allSlots.length} distinct slots!`);

  if (allSlots.length < 1000) {
    throw new Error(`[SETUP FAILED] Insufficient slots provisioned: ${allSlots.length} (expected ~2400)`);
  }

  return {
    adminToken: admin,
    patientToken,
    runId,
    schedules,
    allSlots,
  };
}

export default function (data) {
  if (!data?.allSlots || data.allSlots.length === 0) {
    sleep(1);
    return;
  }

  // Uniform random slot selection among the 2,400 available slots
  // As slots get gradually booked, collisions and conflicts increase naturally.
  const slotIndex = Math.floor(Math.random() * data.allSlots.length);
  const targetSlot = data.allSlots[slotIndex];

  const idempotencyKey = `k6-hl-${data.runId}-vu${__VU}-it${__ITER}`;
  const correlationId = `hl-${data.runId}-${__VU}-${__ITER}`;

  const payload = JSON.stringify({
    workScheduleId: targetSlot.scheduleId,
    timeSlot: targetSlot.timeSlot,
    reason: `High Load Stress ${data.runId} VU ${__VU} Iter ${__ITER}`,
  });

  const headers = {
    ...DEFAULT_HEADERS,
    Authorization: `Bearer ${data.patientToken}`,
    'Idempotency-Key': idempotencyKey,
    'X-Correlation-ID': correlationId,
  };

  totalBookingsAttempted.add(1);
  let appointmentId = null;
  let isCreated = false;

  group('high-load-booking-workflow', () => {
    const e2eStart = Date.now();

    // 1. Post Booking
    const res = http.post(`${BASE_URL}/appointments`, payload, {
      headers,
      tags: { operation: 'high-load-appointment-create' },
    });

    const duration = res.timings.duration;
    bookingApiDuration.add(duration);

    if (res.status === 200 || res.status === 201) {
      const body = JSON.parse(res.body);
      appointmentId = appointmentIdFrom(body);
      isCreated = Boolean(appointmentId);
      bookingCreatedApiDuration.add(duration);
      bookingSuccessRate.add(1);
      serverErrorRate.add(0);
      totalBookingsCreated.add(1);
    } else if (res.status === 409) {
      // Natural conflict when slot is already booked by another VU
      totalConflictsEncountered.add(1);
      bookingConflictApiDuration.add(duration);
      bookingSuccessRate.add(1); // 409 is an expected, graceful business rejection
      serverErrorRate.add(0);
      return;
    } else {
      // Check if 5xx server crash
      const is5xx = res.status >= 500;
      serverErrorRate.add(is5xx ? 1 : 0);
      bookingSuccessRate.add(0);
      return;
    }

    check(res, {
      'booking API returned 200/201 or 409': (r) => r.status === 200 || r.status === 201 || r.status === 409,
    });

    if (!isCreated || !appointmentId) return;

    // 2. Poll until Confirmed (Max 6 seconds under high load)
    let confirmed = false;
    const deadline = Date.now() + 6000;
    while (Date.now() < deadline) {
      const detail = http.get(`${BASE_URL}/appointments/${appointmentId}`, {
        headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${data.patientToken}` },
        tags: { operation: 'high-load-confirmation-poll' },
      });

      if (detail.status === 200) {
        try {
          if (isConfirmed(JSON.parse(detail.body))) {
            confirmed = true;
            break;
          }
        } catch {}
      }
      sleep(0.05); // 50ms rapid polling
    }

    confirmationRate.add(confirmed ? 1 : 0);
    if (confirmed) {
      totalBookingsConfirmed.add(1);
      bookingE2eDuration.add(Date.now() - e2eStart);
    }

    check(confirmed, {
      'appointment reached Confirmed within 6s': (val) => val === true,
    });

    // 3. Retry with same Idempotency-Key
    const retryRes = http.post(`${BASE_URL}/appointments`, payload, {
      headers,
      tags: { operation: 'high-load-idempotency-retry' },
    });

    if (retryRes.status === 200 || retryRes.status === 201) {
      try {
        const retryId = appointmentIdFrom(JSON.parse(retryRes.body));
        const idempotentMatch = retryId === appointmentId;
        idempotencyRate.add(idempotentMatch ? 1 : 0);
        check(idempotentMatch, { 'idempotency returns exact same appointment ID': (v) => v === true });
      } catch {
        idempotencyRate.add(0);
      }
    } else {
      idempotencyRate.add(0);
    }
  });

  // Short paced sleep (20ms - 80ms) to generate ~15,000 requests across 30 VUs in 60s
  sleep(0.02 + Math.random() * 0.06);
}

export function teardown(data) {
  console.log(`[TEARDOWN] Cleaning up ${data.schedules?.length || 0} schedules on ${BASE_URL}...`);
  if (!data?.schedules) return;

  for (const sched of data.schedules) {
    if (sched.createdByTest && sched.id) {
      try {
        cancelWorkSchedule(data.adminToken, { id: sched.id, createdByTest: true });
      } catch (e) {
        // Silently ignore cleanup warning
      }
    }
  }
  console.log('[TEARDOWN] Cleanup completed.');
}
