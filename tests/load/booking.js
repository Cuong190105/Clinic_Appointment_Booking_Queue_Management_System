import http from 'k6/http';
import { check, group, Rate, sleep, Trend } from 'k6';
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
import { availableDoctorSchedules, cancelWorkSchedule, ensureWorkSchedule, login, slotsFromAvailability } from './fixtures.js';

const BOOKING_VUS = Number(__ENV.BOOKING_VUS || 20);
const bookingSuccessRate = new Rate('booking_success_rate');
const confirmationRate = new Rate('booking_confirmed_rate');
const idempotencyRate = new Rate('booking_idempotency_verified_rate');
const cleanupRate = new Rate('booking_cleanup_verified_rate');
const availabilityRate = new Rate('booking_availability_success_rate');
const bookingApiDuration = new Trend('booking_api_duration');
const bookingE2eDuration = new Trend('booking_e2e_duration');

if (!Number.isInteger(BOOKING_VUS) || BOOKING_VUS < 1 || BOOKING_VUS > 40) {
  throw new Error('BOOKING_VUS must be an integer between 1 and 40 (one distinct 15-minute slot per VU).');
}

export const options = {
  scenarios: {
    concurrent_distinct_slot_bookings: {
      executor: 'per-vu-iterations',
      vus: BOOKING_VUS,
      iterations: 1,
      maxDuration: '3m',
    },
  },
  thresholds: {
    booking_success_rate: ['rate>0.98'],
    booking_confirmed_rate: ['rate>0.98'],
    booking_idempotency_verified_rate: ['rate>0.98'],
    booking_cleanup_verified_rate: ['rate>0.98'],
    booking_availability_success_rate: ['rate>0.98'],
    booking_api_duration: ['p(95)<500', 'p(99)<1500'],
    booking_e2e_duration: ['p(95)<5000'],
  },
};

function appointmentIdFrom(body) {
  const appointment = parseAppointment(body);
  return appointment?.id || appointment?.appointmentId || null;
}

function isConfirmed(body) {
  const appointment = parseAppointment(body);
  return appointment?.status === 'Confirmed';
}

export function setup() {
  const adminToken = login(__ENV.BENCHMARK_ADMIN_USERNAME || 'admin1', __ENV.BENCHMARK_ADMIN_PASSWORD || 'Password123!');
  const patientToken = login(PATIENT_USERNAME, PATIENT_PASSWORD);
  const date = clinicDate(1); // Tomorrow: stable future slots independent of the time the test starts.
  const schedule = ensureWorkSchedule(adminToken, date, '08:00:00', '18:00:00', 100);
  const available = availableDoctorSchedules(date);
  const slots = slotsFromAvailability(available, schedule.id);

  if (slots.length < BOOKING_VUS) {
    if (schedule.createdByTest) cancelWorkSchedule(adminToken, schedule);
    throw new Error(
      `Only ${slots.length} available slots were found on ${date}; need ${BOOKING_VUS}. Reduce BOOKING_VUS or use a clean benchmark date.`
    );
  }

  return {
    adminToken,
    patientToken,
    schedule,
    date,
    slots: slots.slice(0, BOOKING_VUS),
    runId: `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`,
  };
}

export default function (data) {
  const slot = data.slots[__VU - 1];
  const idempotencyKey = `k6-booking-${data.runId}-${__VU}`;
  const payload = JSON.stringify({
    workScheduleId: data.schedule.id,
    timeSlot: slot,
    reason: `K6 concurrent booking ${data.runId} VU ${__VU}`,
  });
  const headers = {
    ...DEFAULT_HEADERS,
    Authorization: `Bearer ${data.patientToken}`,
    'Idempotency-Key': idempotencyKey,
    'X-Correlation-ID': `booking-${data.runId}-${__VU}`,
  };

  let appointmentId = null;
  let created = false;
  try {
    group('booking-and-confirmation', () => {
      const start = Date.now();
      const availability = http.get(`${BASE_URL}/doctors/${__ENV.BENCHMARK_DOCTOR_ID || 'a0000004-0000-0000-0000-000000000001'}/available?date=${data.date}`, {
        headers: DEFAULT_HEADERS,
        tags: { operation: 'schedule-availability-before-booking' },
      });
      let selectedSlotAvailable = false;
      if (availability.status === 200) {
        try {
          const schedules = responseItems(JSON.parse(availability.body), ['timeSlot', 'timeSlots']);
          const schedule = schedules.find((item) => item.id === data.schedule.id);
          const availableSlots = schedule?.timeSlot || schedule?.timeSlots || [];
          selectedSlotAvailable = availableSlots.some((value) => String(value).startsWith(slot.slice(0, 5)));
        } catch {
          selectedSlotAvailable = false;
        }
      }
      availabilityRate.add(selectedSlotAvailable);
      check(selectedSlotAvailable, { 'assigned slot is advertised by availability before booking': (value) => value });

      const response = http.post(`${BASE_URL}/appointments`, payload, {
        headers,
        tags: { operation: 'appointment-create' },
      });
      bookingApiDuration.add(response.timings.duration);
      const body = response.status === 200 || response.status === 201 ? JSON.parse(response.body) : null;
      appointmentId = body ? appointmentIdFrom(body) : null;
      created = Boolean(appointmentId);
      bookingSuccessRate.add(created);
      check(response, {
        'booking returns 200/201 with appointment ID': () => created,
      });
      if (!created) return;

      let confirmed = false;
      const deadline = Date.now() + 10_000;
      while (Date.now() < deadline) {
        const detail = http.get(`${BASE_URL}/appointments/${appointmentId}`, {
          headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${data.patientToken}` },
          tags: { operation: 'appointment-confirmation-poll' },
        });
        if (detail.status === 200 && isConfirmed(JSON.parse(detail.body))) {
          confirmed = true;
          break;
        }
        sleep(0.1);
      }
      confirmationRate.add(confirmed);
      if (confirmed) bookingE2eDuration.add(Date.now() - start);
      check(confirmed, { 'appointment reaches Confirmed within 10 seconds': (value) => value });

      const retry = http.post(`${BASE_URL}/appointments`, payload, {
        headers,
        tags: { operation: 'appointment-idempotency-retry' },
      });
      const retryId = retry.status === 200 || retry.status === 201 ? appointmentIdFrom(JSON.parse(retry.body)) : null;
      const idempotent = retryId === appointmentId;
      idempotencyRate.add(idempotent);
      check(idempotent, { 'same idempotency key returns same appointment': (value) => value });
    });
  } finally {
    if (appointmentId) {
      group('test-data-cleanup', () => {
        const canceled = http.post(
          `${BASE_URL}/appointments/${appointmentId}/cancel`,
          JSON.stringify({ reason: 'Automated k6 booking cleanup' }),
          { headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${data.adminToken}` }, tags: { operation: 'appointment-cleanup' } }
        );

        let verified = false;
        if (canceled.status === 200 || canceled.status === 204) {
          for (let attempt = 0; attempt < 10; attempt++) {
            const detail = http.get(`${BASE_URL}/appointments/${appointmentId}`, {
              headers: { ...DEFAULT_HEADERS, Authorization: `Bearer ${data.adminToken}` },
              tags: { operation: 'appointment-cleanup-verify' },
            });
            if (detail.status === 200 && parseAppointment(JSON.parse(detail.body))?.status === 'Cancelled') {
              verified = true;
              break;
            }
            sleep(0.1);
          }
        }
        cleanupRate.add(verified);
        check(verified, { 'test appointment is cancelled after the sample': (value) => value });
      });
    } else {
      cleanupRate.add(true);
    }
  }
}

export function teardown(data) {
  if (data?.schedule?.createdByTest) {
    const canceled = cancelWorkSchedule(data.adminToken, data.schedule);
    check(canceled, { 'dedicated benchmark shift is cancelled after run': (value) => value });
  }
}
