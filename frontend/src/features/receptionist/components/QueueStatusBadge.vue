<script setup lang="ts">
import type { QueueStatus } from '../receptionist.types'

const props = defineProps<{
  status: QueueStatus | string
}>()

function normalizeStatus(s?: string): QueueStatus {
  const str = String(s || '').toLowerCase()
  if (['called', 'inprogress', 'examining', 'beingexamined'].some(k => str.includes(k))) return 'examining'
  if (['completed', 'done', 'finished'].some(k => str.includes(k))) return 'completed'
  return 'waiting'
}
</script>

<template>
  <span
    class="text-xs font-medium px-2.5 py-1 rounded"
    :class="{
      'bg-blue-50 text-blue-700': normalizeStatus(status) === 'examining',
      'bg-amber-50 text-amber-700': normalizeStatus(status) === 'waiting',
      'bg-emerald-50 text-emerald-700': normalizeStatus(status) === 'completed'
    }"
  >
    {{
      normalizeStatus(status) === 'examining'
        ? 'Đang khám'
        : normalizeStatus(status) === 'waiting'
          ? 'Chờ khám'
          : 'Đã khám xong'
    }}
  </span>
</template>
