<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  status?: string | number | boolean | null
  label?: string
}>()

interface StatusConfig {
  text: string
  classes: string
}

const statusMap: Record<string, StatusConfig> = {
  // Boolean
  true: { text: 'Hoạt động', classes: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  false: { text: 'Ngừng hoạt động', classes: 'bg-slate-100 text-slate-500 border-slate-200' },

  // Activity / Employee status
  active: { text: 'Đang hoạt động', classes: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  inactive: { text: 'Ngừng hoạt động', classes: 'bg-slate-100 text-slate-500 border-slate-200' },
  onleave: { text: 'Nghỉ phép', classes: 'bg-amber-50 text-amber-700 border-amber-200' },
  resigned: { text: 'Đã thôi việc', classes: 'bg-rose-50 text-rose-700 border-rose-200' },

  // Appointments / Queue / Reports
  pending: { text: 'Chờ xử lý', classes: 'bg-amber-50 text-amber-700 border-amber-200' },
  confirmed: { text: 'Đã xác nhận', classes: 'bg-blue-50 text-blue-700 border-blue-200' },
  checkedin: { text: 'Đã check-in', classes: 'bg-indigo-50 text-indigo-700 border-indigo-200' },
  inprogress: { text: 'Đang khám', classes: 'bg-purple-50 text-purple-700 border-purple-200' },
  completed: { text: 'Hoàn thành', classes: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  cancelled: { text: 'Đã hủy', classes: 'bg-rose-50 text-rose-700 border-rose-200' },
  noshow: { text: 'Vắng mặt', classes: 'bg-slate-100 text-slate-600 border-slate-200' },
  waiting: { text: 'Đang chờ', classes: 'bg-amber-50 text-amber-700 border-amber-200' },
  called: { text: 'Đã gọi lượt', classes: 'bg-sky-50 text-sky-700 border-sky-200' },
}

const resolvedConfig = computed<StatusConfig>(() => {
  const raw = String(props.status ?? '').toLowerCase().trim()
  if (statusMap[raw]) return statusMap[raw]!

  return {
    text: String(props.status ?? '—'),
    classes: 'bg-slate-100 text-slate-600 border-slate-200',
  }
})
</script>

<template>
  <span
    class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold border transition-colors select-none"
    :class="resolvedConfig.classes"
  >
    <span class="h-1.5 w-1.5 rounded-full bg-current opacity-80" />
    <slot>{{ label || resolvedConfig.text }}</slot>
  </span>
</template>
