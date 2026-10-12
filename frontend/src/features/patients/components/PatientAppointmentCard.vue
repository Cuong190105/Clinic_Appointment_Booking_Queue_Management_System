<script setup lang="ts">
import StatusBadge from '@/components/ui/StatusBadge.vue'
import type { Appointment } from '../patient.types'

const props = defineProps<{
  appointment: Appointment
  cancelling?: boolean
  highlight?: boolean
}>()

const emit = defineEmits<{
  cancel: [id: string | number]
}>()

const canCancel = (status: string) => {
  const s = status.toLowerCase()
  return s === 'pending' || s === 'confirmed'
}
</script>

<template>
  <div
    class="rounded-xl border p-4 transition-all"
    :class="highlight && appointment.status !== 'Cancelled'
      ? 'border-blue-300 bg-blue-50/90 ring-1 ring-blue-300 shadow-2xs'
      : 'border-slate-200 bg-white hover:border-slate-300'"
  >
    <div class="flex items-center gap-4">
      <!-- Date & Time block -->
      <div class="w-20 text-center shrink-0">
        <p class="text-xs font-semibold text-blue-700">{{ appointment.appointmentDate }}</p>
        <p class="text-lg font-bold text-[#0E4D92]">{{ appointment.appointmentTime }}</p>
      </div>

      <div class="h-10 w-px bg-slate-200 shrink-0" />

      <!-- Doctor & Specialty info -->
      <div class="min-w-0 flex-1">
        <div class="flex items-center gap-2">
          <p class="font-bold text-slate-800 truncate">{{ appointment.doctorName }}</p>
          <span
            v-if="highlight && appointment.status !== 'Cancelled'"
            class="rounded-md bg-blue-100 px-1.5 py-0.5 text-[10px] font-bold text-[#0E4D92]"
          >
            Trong 7 ngày
          </span>
        </div>
        <p class="text-xs font-medium text-slate-500 truncate">{{ appointment.specialty }}</p>
      </div>

      <!-- Status & Queue number -->
      <div class="text-right shrink-0 flex flex-col items-end">
        <StatusBadge :status="appointment.status" />
        <p v-if="appointment.queueNumber" class="mt-1 text-xs font-bold text-[#0E4D92]">
          Số: {{ appointment.queueNumber }}
        </p>
      </div>
    </div>

    <!-- Cancel button -->
    <div v-if="canCancel(appointment.status)" class="mt-3 flex justify-end border-t border-slate-100 pt-2.5">
      <button
        type="button"
        :disabled="cancelling"
        class="inline-flex items-center gap-1 text-xs font-semibold text-red-600 hover:text-red-700 hover:underline disabled:opacity-50"
        @click="emit('cancel', appointment.id)"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10"/>
          <line x1="15" y1="9" x2="9" y2="15"/>
          <line x1="9" y1="9" x2="15" y2="15"/>
        </svg>
        <span>{{ cancelling ? 'Đang hủy...' : 'Hủy lịch' }}</span>
      </button>
    </div>
  </div>
</template>
