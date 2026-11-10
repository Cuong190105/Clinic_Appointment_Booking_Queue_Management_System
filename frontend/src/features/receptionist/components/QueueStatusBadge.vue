<script setup lang="ts">
import type { QueueStatus } from '../receptionist.types'

const props = defineProps<{
  status: QueueStatus | string
}>()

function normalizeStatus(s?: string): QueueStatus {
  const str = String(s || '').toLowerCase()
  if (str === 'called') return 'called'
  if (['inprogress', 'examining', 'beingexamined'].some(k => str.includes(k))) return 'inprogress'
  if (['completed', 'done', 'finished'].some(k => str.includes(k))) return 'completed'
  if (str.includes('skipped')) return 'skipped'
  if (str.includes('cancel')) return 'cancelled'
  return 'waiting'
}

const statusStyles: Record<QueueStatus, string> = {
  waiting: 'bg-amber-50 text-amber-700',
  called: 'bg-blue-50 text-blue-700',
  inprogress: 'bg-violet-50 text-violet-700',
  completed: 'bg-emerald-50 text-emerald-700',
  skipped: 'bg-rose-50 text-rose-700',
  cancelled: 'bg-slate-100 text-slate-600',
}

const statusLabels: Record<QueueStatus, string> = {
  waiting: 'Chờ gọi',
  called: 'Đã gọi',
  inprogress: 'Đang khám',
  completed: 'Đã khám xong',
  skipped: 'Đã bỏ qua',
  cancelled: 'Đã hủy',
}
</script>

<template>
  <span
    class="text-xs font-medium px-2.5 py-1 rounded"
    :class="statusStyles[normalizeStatus(status)]"
  >
    {{ statusLabels[normalizeStatus(status)] }}
  </span>
</template>
