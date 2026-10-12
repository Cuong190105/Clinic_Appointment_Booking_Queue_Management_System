<script setup lang="ts">
defineProps<{
  queueNumber: string
  patientName: string
}>()
</script>

<template>
  <div
    class="bg-emerald-50 border border-emerald-200
           rounded-xl p-6 text-center"
  >
    <div class="text-sm font-medium text-emerald-700">
      Check-in thành công
    </div>

    <div
      class="mt-3 text-4xl font-bold
             text-emerald-700 font-mono"
    >
      {{ queueNumber }}
    </div>

    <div class="mt-2 text-sm text-slate-600">
      {{ patientName }}
    </div>

    <div class="mt-1 text-xs text-slate-500">
      Vui lòng chờ đến lượt khám
    </div>
  </div>
</template>
