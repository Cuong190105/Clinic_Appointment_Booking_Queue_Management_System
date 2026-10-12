<script setup lang="ts">
import type { QueuePatient } from '../receptionist.types'
import QueueStatusBadge from './QueueStatusBadge.vue'

defineProps<{
  queue: QueuePatient[]
}>()

const emit = defineEmits<{
  call: [no: string]
  complete: [no: string]
}>()

function handleAction(patient: QueuePatient) {
  if (patient.status === 'waiting') {
    emit('call', patient.no)
  } else if (patient.status === 'examining') {
    emit('complete', patient.no)
  }
}
</script>

<template>
  <div class="border border-slate-200 rounded-lg overflow-hidden">

    <table class="w-full text-sm">

      <!-- HEADER -->
      <thead>
        <tr class="bg-slate-50 border-b border-slate-200">

          <th
            class="py-3 px-4 text-left text-xs
                   font-semibold text-slate-500
                   uppercase tracking-wide"
          >
            STT
          </th>

          <th
            class="py-3 px-4 text-left text-xs
                   font-semibold text-slate-500
                   uppercase tracking-wide"
          >
            Tên bệnh nhân
          </th>

          <th
            class="py-3 px-4 text-left text-xs
                   font-semibold text-slate-500
                   uppercase tracking-wide"
          >
            Bác sĩ
          </th>

          <th
            class="py-3 px-4 text-left text-xs
                   font-semibold text-slate-500
                   uppercase tracking-wide"
          >
            Giờ hẹn
          </th>

          <th
            class="py-3 px-4 text-left text-xs
                   font-semibold text-slate-500
                   uppercase tracking-wide"
          >
            Trạng thái
          </th>

          <th
            class="py-3 px-4 text-left text-xs
                   font-semibold text-slate-500
                   uppercase tracking-wide"
          >
            Thao tác
          </th>

        </tr>
      </thead>

      <!-- BODY -->
      <tbody class="divide-y divide-slate-100">

        <tr v-if="!queue || queue.length === 0">
          <td colspan="6" class="py-4 text-center text-sm text-slate-500">Không có bệnh nhân trong hàng đợi.</td>
        </tr>

        <tr
          v-for="patient in queue"
          :key="patient.no"
          class="hover:bg-slate-50 transition-colors"
        >

          <!-- STT -->
          <td
            class="py-3 px-4 font-mono
                   font-bold text-violet-600"
          >
            {{ patient.no }}
          </td>

          <!-- NAME -->
          <td
            class="py-3 px-4
                   font-medium text-slate-800"
          >
            {{ patient.name }}
          </td>

          <!-- DOCTOR -->
          <td class="py-3 px-4 text-slate-600">
            {{ patient.doctor }}
          </td>

          <!-- TIME -->
          <td
            class="py-3 px-4 text-xs
                   text-slate-500 tabular-nums"
          >
            {{ patient.time }}
          </td>

          <!-- STATUS -->
          <td class="py-3 px-4">
            <QueueStatusBadge
              :status="patient.status"
            />
          </td>

          <!-- ACTION -->
          <td class="py-3 px-4">

            <button
              v-if="patient.status === 'waiting'"
              type="button"
              class="text-xs px-3 py-1
                     bg-violet-600 text-white
                     rounded hover:bg-violet-700
                     transition-colors"
              @click="handleAction(patient)"
            >
              Gọi số
            </button>

            <button
              v-else-if="patient.status === 'examining'"
              type="button"
              class="text-xs px-3 py-1
                     bg-violet-600 text-white
                     rounded hover:bg-violet-700
                     transition-colors"
              @click="handleAction(patient)"
            >
              Hoàn thành
            </button>

            <span
              v-else
              class="text-xs text-slate-400"
            >
              —
            </span>

          </td>

        </tr>

      </tbody>

    </table>

  </div>
</template>
