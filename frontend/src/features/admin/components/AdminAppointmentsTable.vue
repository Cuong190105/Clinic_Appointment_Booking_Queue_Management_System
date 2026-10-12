<script setup lang="ts">
import type { ApptStatus } from '@/features/admin/admin.mock'

defineProps<{
  data: {
    id: number
    patient: string
    doctor: string
    specialty: string
    datetime: string
    status: ApptStatus
  }[]
}>()

function statusText(status: ApptStatus) {
  const map: Record<ApptStatus, string> = {
    completed: 'Hoàn thành',
    waiting: 'Đang chờ',
    cancelled: 'Đã hủy',
    absent: 'Không đến',
  }

  return map[status]
}

function statusClass(status: ApptStatus) {
  const map: Record<ApptStatus, string> = {
    completed: 'bg-green-50 text-green-700',
    waiting: 'bg-blue-50 text-blue-700',
    cancelled: 'bg-red-50 text-red-600',
    absent: 'bg-amber-50 text-amber-700',
  }

  return map[status]
}
</script>

<template>
  <div class="overflow-hidden rounded-lg border border-slate-200">
    <table class="w-full text-sm">
      <thead>
        <tr class="border-b border-slate-200 bg-slate-50">
          <th
            v-for="header in [
              '#',
              'Bệnh nhân',
              'Bác sĩ',
              'Chuyên khoa',
              'Thời gian',
              'Trạng thái',
            ]"
            :key="header"
            class="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500"
          >
            {{ header }}
          </th>
        </tr>
      </thead>

      <tbody class="divide-y divide-slate-100">
        <tr
          v-for="(appointment, index) in data"
          :key="appointment.id"
          class="hover:bg-slate-50"
        >
          <td class="px-4 py-3 text-xs text-slate-400">
            {{ index + 1 }}
          </td>

          <td class="px-4 py-3 font-medium text-slate-800">
            {{ appointment.patient }}
          </td>

          <td class="px-4 py-3 text-slate-600">
            {{ appointment.doctor }}
          </td>

          <td class="px-4 py-3 text-slate-600">
            {{ appointment.specialty }}
          </td>

          <td class="px-4 py-3 text-xs text-slate-500">
            {{ appointment.datetime }}
          </td>

          <td class="px-4 py-3">
            <span
              class="rounded px-2 py-1 text-xs font-medium"
              :class="statusClass(appointment.status)"
            >
              {{ statusText(appointment.status) }}
            </span>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
