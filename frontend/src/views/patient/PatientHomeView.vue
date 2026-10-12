<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { RouterLink } from 'vue-router'

import { useAuthStore } from '@/stores/auth'
import { usePatientStore } from '@/stores/patient'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import PatientAppointmentCard from '@/features/patients/components/PatientAppointmentCard.vue'
import PatientQueueCard from '@/features/patients/components/PatientQueueCard.vue'
import PatientHistoryCard from '@/features/patients/components/PatientHistoryCard.vue'
import PatientBackendNotice from '@/features/patients/components/PatientBackendNotice.vue'

const auth = useAuthStore()
const patient = usePatientStore()

const today = new Date().toLocaleDateString('vi-VN', {
  weekday: 'long',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

const initialLoading = ref(true)
const cancellingAppointmentId = ref<string | number | null>(null)
const appointmentFilter = ref<'all' | 'upcoming' | 'confirmed' | 'pending' | 'cancelled'>('all')

function normalizeStatus(value: unknown): string {
  if (value == null) return ''
  return String(value)
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[_-]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

function isStatusConfirmed(raw: unknown): boolean {
  const s = normalizeStatus(raw)
  return (
    s.includes('confirm') ||
    s.includes('xac') ||
    s.includes('approved') ||
    s.includes('duyet') ||
    s === 'confirmed'
  )
}

function isStatusPending(raw: unknown): boolean {
  const s = normalizeStatus(raw)
  return (
    s.includes('pend') ||
    s.includes('wait') ||
    s.includes('cho') ||
    s.includes('dang cho') ||
    s === 'pending'
  )
}

function isStatusCancelled(raw: unknown): boolean {
  const s = normalizeStatus(raw)
  return s.includes('cancel') || s.includes('huy') || s === 'cancelled' || s === 'canceled'
}

function parseDate(value: unknown): Date | null {
  if (!value && value !== 0) return null
  const s = String(value).trim()
  if (!s) return null

  const dm = /^([0-3]?\d)\/(0?[1-9]|1[0-2])\/(\d{4})$/.exec(s)
  if (dm) {
    const local = new Date(Number(dm[3]), Number(dm[2]) - 1, Number(dm[1]))
    return Number.isNaN(local.getTime()) ? null : local
  }

  const iso = /^(\d{4})-(\d{1,2})-(\d{1,2})/.exec(s)
  if (iso) {
    const local = new Date(Number(iso[1]), Number(iso[2]) - 1, Number(iso[3]))
    return Number.isNaN(local.getTime()) ? null : local
  }

  const parsed = new Date(s)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

function inNext7Days(date: Date): boolean {
  const start = new Date()
  start.setHours(0, 0, 0, 0)
  const end = new Date(start)
  end.setDate(end.getDate() + 6)

  const candidate = new Date(date)
  candidate.setHours(0, 0, 0, 0)
  return candidate >= start && candidate <= end
}

const displayedAppointments = computed(() => {
  const source = Array.isArray(patient.sortedAppointments) && patient.sortedAppointments.length > 0
    ? patient.sortedAppointments
    : (Array.isArray(patient.appointments) ? patient.appointments : (Array.isArray((patient as any).upcomingAppointments) ? (patient as any).upcomingAppointments : []))

  const list = [...source]

  return list
    .filter((appointment) => {
      const status = appointment.status
      const date = parseDate(appointment.appointmentDate)

      switch (appointmentFilter.value) {
        case 'upcoming':
          return date !== null && inNext7Days(date) && (isStatusPending(status) || isStatusConfirmed(status))
        case 'confirmed':
          return isStatusConfirmed(status)
        case 'pending':
          return isStatusPending(status)
        case 'cancelled':
          return isStatusCancelled(status)
        case 'all':
        default:
          return true
      }
    })
    .sort((a, b) => {
      const da = parseDate(a.appointmentDate)
      const db = parseDate(b.appointmentDate)
      if (da && db) return da.getTime() - db.getTime()
      if (da && !db) return -1
      if (!da && db) return 1
      return 0
    })
})

function setFilter(f: typeof appointmentFilter.value) {
  appointmentFilter.value = f
}

// Kiểm tra trạng thái kết nối tới backend và các dịch vụ bệnh nhân
const connecting = computed(() => {
  return (
    initialLoading.value ||
    patient.appointmentsLoading ||
    patient.queueLoading ||
    patient.historyLoading
  )
})

// Kiểm tra xem có bất kỳ lỗi nào từ các dịch vụ bệnh nhân hay không
const hasAnyError = computed(() => {
  return Boolean(
    patient.appointmentsError || patient.queueError || patient.historyError,
  )
})

async function reloadAllData() {
  const tasks = [
    patient.loadAppointments(),
    patient.loadQueue(),
    patient.loadHistory(),
  ]

  try {
    if (!patient.profile && localStorage.getItem('clinic.patient.protected-disabled') !== '1') {
      void patient.loadProfile().catch(() => undefined)
    }
  } catch {
    // ignore
  }

  try {
    await Promise.allSettled(tasks)
  } finally {
    initialLoading.value = false
  }
}

// Tải dữ liệu bệnh nhân khi component được mount
onMounted(async () => {
  await reloadAllData()
})

async function retryAppointments() {
  await patient.loadAppointments()
}

async function retryQueue() {
  await patient.loadQueue()
}

async function retryHistory() {
  await patient.loadHistory()
}

async function handleCancelAppointment(id: string | number) {
  let confirmed = true
  if (typeof window !== 'undefined' && typeof window.confirm === 'function') {
    try {
      confirmed = window.confirm('Bạn có chắc chắn muốn hủy lịch hẹn này không?')
    } catch {
      confirmed = true
    }
  }

  if (confirmed === false) return

  cancellingAppointmentId.value = id
  try {
    await patient.cancelAppointment(id)
  } finally {
    cancellingAppointmentId.value = null
  }
}
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-5">
    <!-- Notice when protected patient endpoints are disabled -->
    <PatientBackendNotice @reconnected="reloadAllData" />

    <!-- Top banners (global) -->
    <BaseAlert
      v-if="connecting"
      type="info"
      :message="initialLoading ? 'Đang kết nối tới dịch vụ bệnh nhân...' : 'Đang đồng bộ dữ liệu...'"
    />

    <BaseAlert
      v-else-if="!connecting && hasAnyError"
      type="warning"
      title="Một hoặc nhiều dịch vụ gặp sự cố"
      message="Hệ thống đang sử dụng dữ liệu tạm. Bạn có thể nhấn 'Thử lại' ở từng khu vực bên dưới."
    />

    <main class="flex-1 space-y-5">
      <!-- Profile summary (always visible) -->
      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-xs">
        <div class="flex items-center justify-between gap-4">
          <div class="flex items-center gap-3 min-w-0">
            <div class="h-12 w-12 rounded-full bg-[#0E4D92] flex items-center justify-center text-white font-bold text-lg shrink-0 shadow-2xs">
              {{ (patient.profile?.fullName ?? auth.user?.name ?? 'B').slice(0, 1).toUpperCase() }}
            </div>
            <div class="min-w-0">
              <p class="text-sm font-bold text-slate-800 truncate">
                {{ patient.profile?.fullName ?? auth.user?.name ?? 'Bệnh nhân' }}
              </p>
              <p class="text-xs text-slate-500 truncate mt-0.5">
                {{ patient.profile?.email ?? auth.user?.email ?? 'Chưa có email' }}
              </p>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <div class="hidden sm:block text-xs text-slate-600">
              Sắp tới (7 ngày): <span class="font-bold text-[#0E4D92]">{{ patient.upcomingWeekCount || 0 }}</span>
            </div>
            <div class="hidden sm:block text-xs text-slate-600">
              Trong hàng đợi: <span class="font-bold text-[#0E4D92]">{{ patient.queue?.myTicket ? 'Có' : 'Chưa' }}</span>
            </div>

            <RouterLink
              to="/patient/booking"
              class="px-3.5 py-1.5 rounded-xl bg-[#0E4D92] text-white text-xs font-semibold hover:bg-[#0b3d75] transition-colors"
            >
              + Đặt khám
            </RouterLink>
          </div>
        </div>

        <div v-if="auth.error" class="mt-3 text-xs text-amber-700 bg-amber-50 p-2.5 rounded-lg border border-amber-200 flex items-center justify-between">
          <span>⚠️ {{ auth.error }}</span>
          <RouterLink to="/profile" class="font-semibold text-[#0E4D92] hover:underline">Hoàn thiện hồ sơ →</RouterLink>
        </div>
      </section>

      <!-- Greeting Banner -->
      <section class="flex items-center justify-between rounded-2xl bg-gradient-to-r from-[#0E4D92] to-[#1a6bbf] p-5 text-white shadow-xs">
        <div>
          <p class="text-xs font-medium text-blue-100">Xin chào,</p>
          <h1 class="text-2xl font-extrabold tracking-tight mt-0.5">
            {{ patient.profile?.fullName || auth.user?.name || 'Bệnh nhân' }} 👋
          </h1>
          <p class="mt-1 text-xs text-blue-100/90">{{ today }}</p>
        </div>

        <div class="flex h-14 w-14 items-center justify-center rounded-2xl bg-white/15 text-3xl shrink-0 backdrop-blur-xs">
          🧑‍⚕️
        </div>
      </section>

      <!-- Quick Actions -->
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <RouterLink
          to="/patient/booking"
          class="rounded-2xl bg-[#0E4D92] p-4 text-white transition-all hover:-translate-y-0.5 hover:shadow-md active:translate-y-0"
        >
          <div class="mb-2 text-2xl">📅</div>
          <p class="text-sm font-bold">Đặt lịch khám</p>
          <p class="text-xs text-blue-200 mt-0.5">Chọn BS & khung giờ</p>
        </RouterLink>

        <RouterLink
          to="/patient/queue"
          class="rounded-2xl bg-[#00A878] p-4 text-white transition-all hover:-translate-y-0.5 hover:shadow-md active:translate-y-0"
        >
          <div class="mb-2 text-2xl">🔢</div>
          <p class="text-sm font-bold">Theo dõi hàng đợi</p>
          <p class="text-xs text-emerald-100 mt-0.5">Xem số thứ tự trực tiếp</p>
        </RouterLink>

        <RouterLink
          to="/patient/history"
          class="rounded-2xl border border-slate-200 bg-white p-4 text-slate-800 transition-all hover:bg-slate-50 hover:-translate-y-0.5 hover:shadow-sm active:translate-y-0"
        >
          <div class="mb-2 text-2xl">📋</div>
          <p class="text-sm font-bold text-slate-800">Lịch sử khám</p>
          <p class="text-xs text-slate-500 mt-0.5">Xem đơn thuốc & kết quả</p>
        </RouterLink>
      </div>

      <!-- Upcoming Appointment Section -->
      <section class="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
        <div class="mb-4 flex items-center justify-between">
          <h2 class="font-bold text-slate-800 text-base">Danh sách lịch hẹn</h2>
          <div class="flex items-center gap-3">
            <RouterLink to="/patient/booking" class="text-xs font-semibold text-[#0E4D92] hover:underline">+ Đặt thêm</RouterLink>
            <button
              v-if="patient.appointmentsError"
              @click="retryAppointments"
              class="text-xs rounded-lg px-2.5 py-1 bg-red-50 text-red-600 border border-red-200 font-semibold hover:bg-red-100"
            >
              Thử lại
            </button>
          </div>
        </div>

        <!-- Filter tabs -->
        <div class="mb-4 flex flex-wrap items-center gap-1.5">
          <button
            type="button"
            class="px-3 py-1 text-xs font-medium rounded-lg transition-colors"
            :class="appointmentFilter === 'all'
              ? 'bg-[#0E4D92] text-white font-semibold shadow-2xs'
              : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
            @click="setFilter('all')"
          >
            Tất cả
          </button>
          <button
            type="button"
            class="px-3 py-1 text-xs font-medium rounded-lg transition-colors"
            :class="appointmentFilter === 'upcoming'
              ? 'bg-[#0E4D92] text-white font-semibold shadow-2xs'
              : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
            @click="setFilter('upcoming')"
          >
            Sắp tới (7 ngày)
          </button>
          <button
            type="button"
            class="px-3 py-1 text-xs font-medium rounded-lg transition-colors"
            :class="appointmentFilter === 'confirmed'
              ? 'bg-[#0E4D92] text-white font-semibold shadow-2xs'
              : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
            @click="setFilter('confirmed')"
          >
            Đã xác nhận
          </button>
          <button
            type="button"
            class="px-3 py-1 text-xs font-medium rounded-lg transition-colors"
            :class="appointmentFilter === 'pending'
              ? 'bg-[#0E4D92] text-white font-semibold shadow-2xs'
              : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
            @click="setFilter('pending')"
          >
            Chờ duyệt
          </button>
          <button
            type="button"
            class="px-3 py-1 text-xs font-medium rounded-lg transition-colors"
            :class="appointmentFilter === 'cancelled'
              ? 'bg-[#0E4D92] text-white font-semibold shadow-2xs'
              : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
            @click="setFilter('cancelled')"
          >
            Đã hủy
          </button>
        </div>

        <div v-if="patient.appointmentsLoading && initialLoading" class="rounded-xl bg-slate-50 p-4 text-sm text-slate-500 text-center">
          Đang tải lịch hẹn...
        </div>

        <div v-else-if="patient.appointmentsError" class="rounded-xl bg-red-50 p-4 text-sm text-red-600 border border-red-200">
          {{ patient.appointmentsError }}
        </div>

        <div v-else-if="!displayedAppointments.length" class="rounded-xl bg-slate-50 p-6 text-sm text-slate-500 text-center">
          Không tìm thấy lịch hẹn phù hợp.
        </div>

        <div v-else class="space-y-3">
          <PatientAppointmentCard
            v-for="appointment in displayedAppointments"
            :key="appointment.id"
            :appointment="appointment"
            :highlight="Array.isArray(patient.upcomingWeekIds) && patient.upcomingWeekIds.some((id) => String(id) === String(appointment.id))"
            :cancelling="cancellingAppointmentId === appointment.id"
            @cancel="handleCancelAppointment"
          />
        </div>
      </section>

      <!-- Queue Status Section -->
      <section class="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
        <div class="flex items-center justify-between mb-4">
          <h2 class="font-bold text-slate-800 text-base">Trạng thái hàng đợi</h2>
          <div>
            <button
              v-if="patient.queueError"
              @click="retryQueue"
              class="text-xs rounded-lg px-2.5 py-1 bg-red-50 text-red-600 border border-red-200 font-semibold hover:bg-red-100"
            >
              Thử lại
            </button>
          </div>
        </div>

        <div v-if="patient.queueLoading && initialLoading" class="py-6 text-center text-sm text-slate-400">
          Đang tải hàng đợi...
        </div>

        <div v-else-if="patient.queueError" class="rounded-xl border border-red-200 bg-red-50 p-6 text-center text-sm text-red-600">
          {{ patient.queueError }}
        </div>

        <PatientQueueCard
          v-else
          :queue="patient.queue"
          :show-details-link="true"
        />
      </section>

      <!-- Recent History Section -->
      <section class="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
        <div class="mb-4 flex items-center justify-between">
          <h2 class="font-bold text-slate-800 text-base">Lịch khám gần đây</h2>
          <div class="flex items-center gap-3">
            <RouterLink to="/patient/history" class="text-xs font-semibold text-[#0E4D92] hover:underline">Xem tất cả →</RouterLink>
            <button
              v-if="patient.historyError"
              @click="retryHistory"
              class="text-xs rounded-lg px-2.5 py-1 bg-red-50 text-red-600 border border-red-200 font-semibold hover:bg-red-100"
            >
              Thử lại
            </button>
          </div>
        </div>

        <div v-if="patient.historyLoading && initialLoading" class="py-6 text-center text-sm text-slate-400">
          Đang tải lịch sử khám...
        </div>

        <div v-else-if="patient.historyError" class="rounded-xl border border-red-200 bg-red-50 p-6 text-center text-sm text-red-600">
          {{ patient.historyError }}
        </div>

        <div v-else-if="!patient.history || patient.history.length === 0" class="rounded-2xl border border-slate-200 bg-white p-6 text-center text-sm text-slate-400">
          Chưa có lịch sử khám bệnh nào được ghi nhận.
        </div>

        <div v-else class="space-y-4">
          <PatientHistoryCard
            v-for="record in patient.history.slice(0, 3)"
            :key="record.id"
            :record="record"
          />
        </div>
      </section>
    </main>
  </div>
</template>