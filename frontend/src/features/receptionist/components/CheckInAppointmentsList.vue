<script setup lang="ts">
import { computed } from 'vue'
import type { AppointmentItem } from '../receptionist.types'
import BaseButton from '@/components/ui/BaseButton.vue'

const props = defineProps<{
  appointments: AppointmentItem[]
  loading?: boolean
  actionLoadingId?: string | null
}>()

const emit = defineEmits<{
  (e: 'confirm', appt: AppointmentItem): void
  (e: 'check-in', appt: AppointmentItem): void
}>()

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

function isToday(dateStr?: string | null): boolean {
  if (!dateStr) return false
  const apptDate = dateStr.includes('T') ? dateStr.slice(0, 10) : dateStr
  const now = new Date()
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
  return apptDate === today
}

function normalizeStatus(s?: string | number | null): string {
  const str = String(s ?? '').trim().toLowerCase()
  if (str === '0' || str.includes('pending')) return 'Pending'
  if (str === '1' || str.includes('confirmed')) return 'Confirmed'
  if (str === '2' || str.includes('checkin') || str.includes('checkedin')) return 'CheckedIn'
  if (str === '3' || str.includes('completed')) return 'Completed'
  if (str === '4' || str.includes('cancelled')) return 'Cancelled'
  return 'Pending'
}

function statusBadgeClass(status: string) {
  const norm = normalizeStatus(status)
  if (norm === 'Pending') return 'bg-amber-50 text-amber-700 border-amber-200'
  if (norm === 'Confirmed') return 'bg-blue-50 text-[#0E4D92] border-blue-200'
  if (norm === 'CheckedIn') return 'bg-purple-50 text-purple-700 border-purple-200'
  if (norm === 'Completed') return 'bg-emerald-50 text-emerald-700 border-emerald-200'
  return 'bg-slate-100 text-slate-600 border-slate-200'
}

function statusLabel(status: string) {
  const norm = normalizeStatus(status)
  if (norm === 'Pending') return 'Chờ xác nhận'
  if (norm === 'Confirmed') return 'Đã xác nhận'
  if (norm === 'CheckedIn') return 'Đã check-in'
  if (norm === 'Completed') return 'Đã khám xong'
  if (norm === 'Cancelled') return 'Đã hủy'
  return status
}
</script>

<template>
  <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-2xs space-y-4">
    <div>
      <h2 class="text-sm font-bold uppercase tracking-wider text-slate-700">
        3. Lịch hẹn khám của bệnh nhân
      </h2>
      <p class="text-xs text-slate-500 mt-0.5">
        Chọn lịch hẹn tương ứng để tiến hành Xác nhận hoặc Check-in cấp số thứ tự
      </p>
    </div>

    <!-- LOADING -->
    <div v-if="loading" class="py-8 text-center text-xs font-semibold text-violet-600">
      <span class="animate-spin inline-block text-xl mb-1">🌀</span>
      <div>Đang tải danh sách lịch hẹn...</div>
    </div>

    <!-- APPOINTMENTS LIST -->
    <div v-else-if="appointments.length > 0" class="space-y-3">
      <div
        v-for="appt in appointments"
        :key="appt.id"
        class="rounded-xl border p-4 transition-all"
        :class="
          isToday(appt.date)
            ? 'border-[#0E4D92] bg-blue-50/20 shadow-2xs'
            : 'border-slate-200 hover:border-slate-300'
        "
      >
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div class="space-y-1.5">
            <div class="flex items-center flex-wrap gap-2">
              <span class="text-sm font-bold text-slate-800">
                📅 {{ formatDate(appt.date) }}
              </span>

              <span
                v-if="isToday(appt.date)"
                class="rounded-md bg-emerald-50 px-2 py-0.5 text-[11px] font-bold text-emerald-700 border border-emerald-200"
              >
                Hôm nay
              </span>
              <span
                v-else
                class="rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-500"
              >
                Lịch tương lai
              </span>

              <span class="font-mono text-xs font-bold text-[#0E4D92] bg-blue-50 px-2 py-0.5 rounded-md">
                🕒 {{ formatTime(appt.timeSlot) }}
              </span>

              <span
                class="rounded-full border px-2 py-0.5 text-[11px] font-semibold"
                :class="statusBadgeClass(String(appt.status ?? ''))"
              >
                {{ statusLabel(String(appt.status ?? '')) }}
              </span>
            </div>

            <div class="text-xs text-slate-600 flex flex-wrap gap-x-4">
              <span>Bác sĩ: <b class="text-slate-800">{{ appt.doctorName || 'Chưa phân công' }}</b></span>
              <span v-if="appt.specialtyName">Chuyên khoa: <b>{{ appt.specialtyName }}</b></span>
            </div>

            <div v-if="appt.reason" class="text-xs text-slate-500 italic">
              Lý do: {{ appt.reason }}
            </div>
          </div>

          <!-- ACTIONS -->
          <div class="flex flex-col sm:items-end gap-1.5 self-end sm:self-center">
            <!-- Nút Xác nhận nếu Pending -->
            <BaseButton
              v-if="normalizeStatus(appt.status) === 'Pending'"
              variant="secondary"
              size="sm"
              :disabled="actionLoadingId === appt.id"
              @click="emit('confirm', appt)"
            >
              <span>{{ actionLoadingId === appt.id ? 'Đang xử lý...' : '✓ Xác nhận' }}</span>
            </BaseButton>

            <!-- Nút Check-in nếu Confirmed -->
            <template v-else-if="normalizeStatus(appt.status) === 'Confirmed'">
              <BaseButton
                variant="primary"
                size="sm"
                :disabled="actionLoadingId === appt.id || !isToday(appt.date)"
                :title="!isToday(appt.date) ? 'Chỉ được check-in vào đúng ngày hẹn khám' : ''"
                @click="emit('check-in', appt)"
              >
                <span>{{ actionLoadingId === appt.id ? 'Đang cấp số...' : '🎫 Check-in' }}</span>
              </BaseButton>
              <span v-if="!isToday(appt.date)" class="text-[10px] text-amber-600 font-medium">
                ⚠️ Chỉ check-in vào ngày khám
              </span>
            </template>

            <!-- Đã Check-in -->
            <span
              v-else-if="normalizeStatus(appt.status) === 'CheckedIn'"
              class="inline-flex items-center gap-1 rounded-lg bg-purple-50 px-3 py-1.5 text-xs font-bold text-purple-700 border border-purple-200"
            >
              <span>✓ Đã nhận số thứ tự</span>
            </span>

            <span v-else class="text-xs text-slate-400 italic">
              Không khả dụng
            </span>
          </div>
        </div>
      </div>
    </div>

    <!-- EMPTY -->
    <div
      v-else
      class="rounded-xl border border-dashed border-slate-200 p-8 text-center text-xs text-slate-400"
    >
      <div class="text-2xl mb-1">📅</div>
      <div class="font-medium text-slate-600">Bệnh nhân này chưa có lịch hẹn khám sắp tới nào</div>
      <div class="mt-1 text-slate-400">Vui lòng tạo lịch hẹn mới cho bệnh nhân nếu cần khám hôm nay</div>
    </div>
  </div>
</template>
