<script setup lang="ts">
defineProps<{
  queueNumber: string
  patientName: string
  doctorName: string
  checkInTime: string
  pending: boolean
  loading: boolean
}>()

const emit = defineEmits<{ retry: [] }>()

function printTicket() {
  window.print()
}
</script>

<template>
  <div
    class="ticket-print-area bg-white border-2 border-violet-600 rounded-xl p-6 text-center"
  >
    <div class="text-sm font-bold text-violet-700">
      PHIẾU KHÁM BỆNH
    </div>

    <div
      class="mt-3 text-5xl font-bold text-violet-700 font-mono"
    >
      {{ queueNumber }}
    </div>

    <div class="mt-2 text-sm text-slate-600">
      {{ patientName }}
    </div>

    <div v-if="doctorName" class="mt-1 text-sm text-slate-600">BS. {{ doctorName }}</div>
    <div v-if="!pending && checkInTime" class="mt-1 text-xs text-slate-500">
      {{ new Date(checkInTime).toLocaleString('vi-VN') }}
    </div>

    <div v-if="pending" class="mt-3 text-sm font-medium text-amber-700">
      Đang chờ Queue cấp số; vui lòng đợi trước khi tiếp nhận người tiếp theo.
    </div>
    <div v-else class="mt-1 text-xs text-slate-500">
      Vui lòng chờ đến lượt khám
    </div>

    <div v-if="pending" class="no-print mt-4">
      <button class="rounded border px-3 py-2 text-sm" :disabled="loading" @click="emit('retry')">
        {{ loading ? 'Đang kiểm tra…' : 'Kiểm tra lại phiếu' }}
      </button>
    </div>
    <div v-else class="no-print mt-4">
      <button class="rounded border px-3 py-2 text-sm" @click="printTicket">In phiếu khám</button>
    </div>
  </div>
</template>

<style scoped>
@media print {
  :global(body *) { visibility: hidden !important; }
  .ticket-print-area, .ticket-print-area * { visibility: visible !important; }
  .ticket-print-area { position: absolute; inset: 0; width: 100%; border: 0; }
  .no-print { display: none !important; }
}
</style>
