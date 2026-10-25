<script setup lang="ts">
import type { QueuePatient } from '../receptionist.types'
import QueueStatusBadge from './QueueStatusBadge.vue'

defineProps<{
  queue: QueuePatient[]
  loading?: boolean
}>()

const emit = defineEmits<{
  call: [ticketId: string]
  skip: [ticketId: string]
  retry: [ticketId: string]
  priority: [ticketId: string, value: boolean]
}>()
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
          :key="patient.id || patient.no"
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
              v-if="patient.status === 'waiting' && patient.id"
              type="button"
              class="mr-1 rounded bg-violet-600 px-3 py-1 text-xs text-white transition-colors hover:bg-violet-700 disabled:opacity-50"
              :disabled="loading"
              @click="emit('call', patient.id!)"
            >
              Gọi số
            </button>

            <button
              v-if="patient.status === 'waiting' && patient.id"
              type="button"
              class="mr-1 rounded border border-slate-300 px-3 py-1 text-xs text-slate-700 transition-colors hover:bg-slate-100 disabled:opacity-50"
              :disabled="loading"
              @click="emit('skip', patient.id!)"
            >
              Bỏ qua
            </button>

            <button
              v-if="patient.status === 'waiting' && patient.id"
              type="button"
              class="rounded px-2 py-1 text-xs font-semibold transition-colors disabled:opacity-50"
              :class="patient.priority ? 'text-amber-700 hover:bg-amber-50' : 'text-slate-500 hover:bg-slate-100'"
              :disabled="loading"
              @click="emit('priority', patient.id!, !patient.priority)"
            >
              {{ patient.priority ? '★ Ưu tiên' : '☆ Ưu tiên' }}
            </button>

            <button
              v-else-if="patient.status === 'skipped' && patient.id"
              type="button"
              class="rounded bg-rose-600 px-3 py-1 text-xs text-white transition-colors hover:bg-rose-700 disabled:opacity-50"
              :disabled="loading"
              @click="emit('retry', patient.id!)"
            >
              Gọi lại
            </button>

            <span v-else class="text-xs text-slate-400">—</span>

          </td>

        </tr>

      </tbody>

    </table>

  </div>
</template>
