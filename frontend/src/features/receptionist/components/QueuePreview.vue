<script setup lang="ts">
import type { QueuePatient } from '../receptionist.types'

defineProps<{
  queue: QueuePatient[]
}>()

function statusText(status: QueuePatient['status']) {
  if (status === 'examining') return 'Đang khám'
  if (status === 'completed') return 'Đã khám'
  return 'Chờ'
}
</script>

<template>
  <div
    class="bg-white border border-slate-200
           rounded-xl p-6"
  >
    <h3 class="text-sm font-semibold text-slate-800 mb-4">
      Hàng đợi hiện tại
    </h3>

    <div class="space-y-2">

      <div v-if="!queue || queue.length === 0" class="py-6 text-center text-sm text-slate-500">
        Không có bệnh nhân trong hàng chờ.
      </div>

      <div
        v-for="patient in queue.slice(0, 4)"
        :key="patient.no"
        class="flex items-center gap-3 py-2
               border-b border-slate-100
               last:border-0"
      >

        <!-- STT -->
        <span
          class="font-mono text-sm font-bold
                 text-violet-600 w-14"
        >
          {{ patient.no }}
        </span>

        <!-- Patient -->
        <div class="flex-1">

          <div
            class="text-sm font-medium
                   text-slate-800"
          >
            {{ patient.name }}
          </div>

          <div class="text-xs text-slate-400">
            {{ patient.doctor }} · {{ patient.time }}
          </div>

        </div>

        <!-- Status -->
        <span
          class="text-xs font-medium
                 px-2.5 py-1 rounded"
          :class="{
            'bg-blue-50 text-blue-700':
              patient.status === 'examining',

            'bg-slate-100 text-slate-500':
              patient.status === 'waiting',

            'bg-emerald-50 text-emerald-700':
              patient.status === 'completed'
          }"
        >
          {{ statusText(patient.status) }}
        </span>

      </div>

    </div>
  </div>
</template>
