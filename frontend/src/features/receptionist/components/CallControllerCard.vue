<script setup lang="ts">
import type { QueueTicketDto, ReceptionistDoctor } from '../receptionist.types'
import QueueStatusBadge from './QueueStatusBadge.vue'

defineProps<{
  doctor: ReceptionistDoctor | null
  currentTicket: QueueTicketDto | null
  nextTicket: QueueTicketDto | null
  waitingCount: number
  loading?: boolean
}>()

const emit = defineEmits<{
  'call-next': []
  'start-exam': [ticketId: string]
  'complete-exam': [ticketId: string]
  'skip': [ticketId: string]
}>()

function formatTime(timeStr?: string | null): string {
  if (!timeStr) return '--:--'
  try {
    const d = new Date(timeStr)
    if (isNaN(d.getTime())) return timeStr.slice(11, 16) || timeStr
    return d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
  } catch {
    return timeStr
  }
}
</script>

<template>
  <div class="overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-sm">
    <!-- CARD HEADER -->
    <div class="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 bg-gradient-to-r from-slate-50 to-violet-50/30 px-6 py-4">
      <div class="flex items-center gap-3">
        <div class="flex h-10 w-10 items-center justify-center rounded-xl bg-violet-600 text-lg text-white shadow-md shadow-violet-500/20">
          🔊
        </div>
        <div>
          <h2 class="text-sm font-bold text-slate-800">
            Bảng Điều khiển Gọi số & Điều phối Trực tiếp
          </h2>
          <p class="text-xs text-slate-500">
            Phòng khám:
            <span class="font-semibold text-violet-700">
              {{ doctor?.roomNumber ?? 'Chưa chọn' }} - {{ doctor?.fullName ?? 'Vui lòng chọn bác sĩ' }}
            </span>
            <span v-if="doctor?.specialtyName" class="text-slate-400"> ({{ doctor.specialtyName }})</span>
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2">
        <span class="inline-flex items-center gap-1.5 rounded-full bg-amber-50 px-3 py-1 text-xs font-bold text-amber-700 border border-amber-200">
          <span>⏳</span>
          <span>{{ waitingCount }} người đang chờ</span>
        </span>
      </div>
    </div>

    <!-- MAIN CONTROLLER GRID -->
    <div class="grid grid-cols-1 divide-y divide-slate-100 lg:grid-cols-2 lg:divide-x lg:divide-y-0">
      <!-- LEFT COLUMN: CURRENTLY EXAMINING / CALLED PATIENT -->
      <div class="p-6">
        <div class="mb-3 flex items-center justify-between">
          <div class="flex items-center gap-2">
            <span class="flex h-2.5 w-2.5 rounded-full bg-blue-500 animate-ping" v-if="currentTicket" />
            <h3 class="text-xs font-bold uppercase tracking-wider text-slate-500">
              Bệnh nhân đang khám / đang gọi
            </h3>
          </div>
          <QueueStatusBadge v-if="currentTicket" :status="currentTicket.status" />
        </div>

        <!-- ACTIVE TICKET VIEW -->
        <div
          v-if="currentTicket"
          class="rounded-xl border border-blue-100 bg-gradient-to-br from-blue-50/50 via-white to-violet-50/30 p-5"
        >
          <div class="flex items-start justify-between">
            <div>
              <div class="flex items-center gap-2">
                <span class="font-mono text-3xl font-extrabold text-blue-700">
                  #{{ String(currentTicket.queueNumber).padStart(3, '0') }}
                </span>
                <span
                  v-if="currentTicket.priority"
                  class="inline-flex items-center gap-1 rounded-md bg-amber-500 px-2 py-0.5 text-[11px] font-bold text-white shadow-sm"
                >
                  ⭐ Ưu tiên
                </span>
              </div>
              <h4 class="mt-2 text-base font-bold text-slate-800">
                {{ currentTicket.patientName || 'Bệnh nhân' }}
              </h4>
              <div class="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500">
                <span>🕒 Check-in: <b>{{ formatTime(currentTicket.checkInTime) }}</b></span>
                <span v-if="currentTicket.calledAt">
                  📢 Gọi lúc: <b>{{ formatTime(currentTicket.calledAt) }}</b>
                </span>
              </div>
            </div>
          </div>

          <!-- ACTIONS FOR CURRENT PATIENT -->
          <div class="mt-5 flex flex-wrap items-center gap-2 pt-3 border-t border-slate-100">
            <!-- If called, can start exam -->
            <button
              v-if="currentTicket.status === 'Called' || (currentTicket.status as string) === 'calling'"
              type="button"
              :disabled="loading"
              class="inline-flex items-center gap-1.5 rounded-lg bg-blue-600 px-3.5 py-2 text-xs font-bold text-white shadow-sm hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500/20 disabled:opacity-50 transition-all cursor-pointer"
              @click="emit('start-exam', currentTicket.id)"
            >
              <span>🩺</span>
              <span>Bắt đầu khám</span>
            </button>

            <!-- Complete Exam button -->
            <button
              type="button"
              :disabled="loading"
              class="inline-flex items-center gap-1.5 rounded-lg bg-emerald-600 px-4 py-2 text-xs font-bold text-white shadow-sm hover:bg-emerald-700 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 disabled:opacity-50 transition-all cursor-pointer"
              @click="emit('complete-exam', currentTicket.id)"
            >
              <span>✅</span>
              <span>Hoàn thành khám</span>
            </button>

            <!-- Skip current patient -->
            <button
              type="button"
              :disabled="loading"
              class="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-600 hover:border-rose-200 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50 transition-all cursor-pointer"
              @click="emit('skip', currentTicket.id)"
            >
              <span>⏭️</span>
              <span>Bỏ qua (Vắng mặt)</span>
            </button>
          </div>
        </div>

        <!-- EMPTY STATE FOR CURRENT PATIENT -->
        <div
          v-else
          class="flex flex-col items-center justify-center rounded-xl border border-dashed border-slate-200 bg-slate-50/50 p-8 text-center"
        >
          <div class="flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-2xl text-slate-400">
            🩺
          </div>
          <p class="mt-2 text-xs font-bold text-slate-600">
            Phòng khám hiện đang trống
          </p>
          <p class="mt-0.5 text-xs text-slate-400">
            Chưa có ca khám nào đang diễn ra. Bấm "Gọi số tiếp theo" để đón bệnh nhân.
          </p>
        </div>
      </div>

      <!-- RIGHT COLUMN: NEXT IN QUEUE & CALL NEXT ACTION -->
      <div class="p-6">
        <div class="mb-3 flex items-center justify-between">
          <h3 class="text-xs font-bold uppercase tracking-wider text-slate-500">
            Tiếp theo trong hàng đợi
          </h3>
          <span v-if="nextTicket" class="text-xs text-slate-400">Đang chờ gọi</span>
        </div>

        <!-- NEXT TICKET VIEW -->
        <div
          v-if="nextTicket"
          class="rounded-xl border border-violet-100 bg-gradient-to-br from-violet-50/40 via-white to-amber-50/30 p-5"
        >
          <div class="flex items-start justify-between">
            <div>
              <div class="flex items-center gap-2">
                <span class="font-mono text-3xl font-extrabold text-violet-700">
                  #{{ String(nextTicket.queueNumber).padStart(3, '0') }}
                </span>
                <span
                  v-if="nextTicket.priority"
                  class="inline-flex items-center gap-1 rounded-md bg-amber-500 px-2 py-0.5 text-[11px] font-bold text-white shadow-sm"
                >
                  ⭐ Ưu tiên
                </span>
              </div>
              <h4 class="mt-2 text-base font-bold text-slate-800">
                {{ nextTicket.patientName || 'Bệnh nhân kế tiếp' }}
              </h4>
              <div class="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500">
                <span>🕒 Check-in lúc: <b>{{ formatTime(nextTicket.checkInTime) }}</b></span>
              </div>
            </div>
          </div>

          <!-- ACTIONS FOR NEXT PATIENT -->
          <div class="mt-5 flex flex-wrap items-center gap-2 pt-3 border-t border-slate-100">
            <button
              type="button"
              :disabled="loading || !doctor"
              class="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-violet-600 to-indigo-600 px-5 py-2.5 text-xs font-bold text-white shadow-md shadow-violet-500/25 hover:from-violet-700 hover:to-indigo-700 focus:outline-none focus:ring-2 focus:ring-violet-500/30 active:scale-[0.98] disabled:opacity-50 transition-all cursor-pointer"
              @click="emit('call-next')"
            >
              <span>📢</span>
              <span>GỌI SỐ TIẾP THEO</span>
            </button>

            <button
              type="button"
              :disabled="loading"
              class="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-600 hover:border-rose-200 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50 transition-all cursor-pointer"
              @click="emit('skip', nextTicket.id)"
            >
              <span>⏭️</span>
              <span>Bỏ qua</span>
            </button>
          </div>
        </div>

        <!-- EMPTY STATE FOR NEXT PATIENT -->
        <div
          v-else
          class="flex flex-col items-center justify-center rounded-xl border border-dashed border-slate-200 bg-slate-50/50 p-8 text-center"
        >
          <div class="flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-2xl text-slate-400">
            🎉
          </div>
          <p class="mt-2 text-xs font-bold text-slate-600">
            Hết bệnh nhân trong hàng chờ
          </p>
          <p class="mt-0.5 text-xs text-slate-400">
            Tất cả bệnh nhân check-in cho phòng khám này đã được phục vụ.
          </p>

          <button
            v-if="doctor"
            type="button"
            :disabled="loading"
            class="mt-4 inline-flex items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50 cursor-pointer"
            @click="emit('call-next')"
          >
            <span>📢</span>
            <span>Thử gọi số kế</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
