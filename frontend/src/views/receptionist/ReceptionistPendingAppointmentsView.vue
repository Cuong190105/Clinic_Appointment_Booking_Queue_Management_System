<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { receptionistApi } from '@/features/receptionist/receptionist.api'
import type { AppointmentItem } from '@/features/receptionist/receptionist.types'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import AdminPagination from '@/features/admin/components/AdminPagination.vue'

const appointments = ref<AppointmentItem[]>([])
const pageNumber = ref(1)
const pageSize = ref(10)
const total = ref(0)
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)
const rowLoading = ref<Record<string, boolean>>({})

function formatDate(value?: string | null): string {
  if (!value) return '—'
  const parsed = new Date(value.includes('T') ? value : `${value}T00:00:00`)
  if (Number.isNaN(parsed.getTime())) return value
  return parsed.toLocaleDateString('vi-VN', { weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' })
}

function formatTime(value: unknown): string {
  if (!value) return '—'
  if (typeof value === 'string') return value.includes(':') ? value.slice(0, 5) : value
  if (typeof value === 'object' && value && 'hours' in value) {
    const v = value as { hours?: number; minutes?: number }
    return `${String(v.hours ?? 0).padStart(2, '0')}:${String(v.minutes ?? 0).padStart(2, '0')}`
  }
  return String(value)
}

async function loadPending() {
  isLoading.value = true
  errorMessage.value = null
  try {
    const res = await receptionistApi.getPendingAppointments({
      pageNumber: pageNumber.value,
      pageSize: pageSize.value,
      sortBy: 'date',
      orderBy: 'asc',
    })
    appointments.value = res.items || []
    total.value = res.totalCount || appointments.value.length
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || err.message || 'Không thể tải danh sách chờ xác nhận.'
  } finally {
    isLoading.value = false
  }
}

async function handleConfirm(appt: AppointmentItem) {
  errorMessage.value = null
  successMessage.value = null

  if (!appt.patientName || !appt.doctorId || !appt.date) {
    errorMessage.value = 'Cuộc hẹn thiếu dữ liệu (tên bệnh nhân/bác sĩ/ngày). Không thể xác nhận.'
    return
  }

  rowLoading.value[appt.id] = true
  try {
    await receptionistApi.confirmAppointment(appt.id)
    successMessage.value = `Đã xác nhận cuộc hẹn của bệnh nhân ${appt.patientName || ''} thành công.`
    // Remove from local list
    appointments.value = appointments.value.filter((a) => a.id !== appt.id)
    total.value = Math.max(0, total.value - 1)
  } catch (err: any) {
    const status = err.response?.status
    if (status === 409) {
      await loadPending()
      errorMessage.value = err.response?.data?.message || 'Xung đột: cuộc hẹn không thể được xác nhận hoặc đã được xử lý.'
    } else {
      errorMessage.value = err.response?.data?.message || err.message || 'Xác nhận thất bại. Vui lòng thử lại.'
    }
  } finally {
    rowLoading.value[appt.id] = false
  }
}

function onPageChange(page: number) {
  pageNumber.value = page
  void loadPending()
}

onMounted(() => {
  void loadPending()
})
</script>

<template>
  <div class="space-y-5 max-w-5xl">
    <!-- HEADER -->
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
      <div>
        <h1 class="text-xl font-bold text-slate-800">
          Danh sách lịch hẹn chờ xác nhận
        </h1>
        <p class="mt-1 text-xs sm:text-sm text-slate-500">
          Xác nhận các yêu cầu đặt lịch trực tuyến của bệnh nhân để chuyển sang trạng thái sẵn sàng khám
        </p>
      </div>

      <div>
        <BaseButton
          variant="secondary"
          :disabled="isLoading"
          @click="loadPending"
        >
          <span>🔄</span>
          <span>Làm mới</span>
        </BaseButton>
      </div>
    </div>

    <!-- NOTICES -->
    <BaseAlert
      v-if="errorMessage"
      type="error"
      :message="errorMessage"
      dismissible
      @dismiss="errorMessage = null"
    />
    <BaseAlert
      v-if="successMessage"
      type="success"
      :message="successMessage"
      dismissible
      @dismiss="successMessage = null"
    />

    <!-- MAIN TABLE CARD -->
    <div class="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xs">
      <div class="overflow-x-auto">
        <table class="w-full text-sm">
          <thead>
            <tr class="border-b border-slate-200 bg-slate-50 text-slate-600">
              <th class="px-5 py-3.5 text-left text-xs font-bold uppercase tracking-wider">Ngày khám</th>
              <th class="px-4 py-3.5 text-left text-xs font-bold uppercase tracking-wider">Khung giờ</th>
              <th class="px-5 py-3.5 text-left text-xs font-bold uppercase tracking-wider">Bệnh nhân</th>
              <th class="px-5 py-3.5 text-left text-xs font-bold uppercase tracking-wider">Bác sĩ khám</th>
              <th class="px-4 py-3.5 text-left text-xs font-bold uppercase tracking-wider">Lý do khám</th>
              <th class="px-5 py-3.5 text-right text-xs font-bold uppercase tracking-wider">Hành động</th>
            </tr>
          </thead>

          <tbody class="divide-y divide-slate-100">
            <tr
              v-for="a in appointments"
              :key="a.id"
              class="hover:bg-slate-50/70 transition-colors"
            >
              <td class="px-5 py-4 font-semibold text-slate-800">
                {{ formatDate(a.date) }}
              </td>
              <td class="px-4 py-4">
                <span class="inline-flex items-center rounded-md bg-violet-50 px-2 py-1 font-mono text-xs font-bold text-violet-700">
                  {{ formatTime(a.timeSlot) }}
                </span>
              </td>
              <td class="px-5 py-4">
                <div class="font-bold text-slate-800">{{ a.patientName ?? '—' }}</div>
              </td>
              <td class="px-5 py-4">
                <div class="font-medium text-slate-700">{{ a.doctorName ?? '—' }}</div>
              </td>
              <td class="px-4 py-4 max-w-xs text-xs text-slate-500 truncate" :title="a.reason ?? ''">
                {{ a.reason || '—' }}
              </td>
              <td class="px-5 py-4 text-right">
                <BaseButton
                  variant="primary"
                  size="sm"
                  :disabled="isLoading || !!rowLoading[a.id]"
                  @click="handleConfirm(a)"
                >
                  <span>{{ rowLoading[a.id] ? 'Đang xử lý...' : '✓ Xác nhận' }}</span>
                </BaseButton>
              </td>
            </tr>

            <!-- EMPTY STATE -->
            <tr v-if="appointments.length === 0 && !isLoading">
              <td colspan="6" class="py-12 text-center text-slate-400">
                <div class="text-3xl mb-2">📋</div>
                <div class="font-medium text-slate-600">Không có lịch hẹn nào đang chờ xác nhận</div>
                <div class="text-xs text-slate-400 mt-1">Các yêu cầu đặt khám mới từ bệnh nhân sẽ xuất hiện tại đây</div>
              </td>
            </tr>

            <!-- LOADING STATE -->
            <tr v-if="isLoading">
              <td colspan="6" class="py-12 text-center text-slate-500">
                <span class="animate-spin inline-block text-2xl mb-1">🌀</span>
                <div class="text-xs font-semibold text-violet-600">Đang tải danh sách chờ xác nhận...</div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- PAGINATION -->
      <div v-if="total > 0" class="border-t border-slate-100 p-4">
        <AdminPagination
          :current-page="pageNumber"
          :total-pages="Math.ceil(total / pageSize) || 1"
          :total-count="total"
          @update:current-page="onPageChange"
        />
      </div>
    </div>
  </div>
</template>
