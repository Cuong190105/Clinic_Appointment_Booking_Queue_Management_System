<script setup lang="ts">
defineProps<{
  label: string
  value: string | number
  color: string
}>()
</script>

<template>
  <div
    class="bg-white border border-slate-200
           rounded-xl p-4"
  >
    <div
      class="text-2xl font-bold mb-0.5"
      :style="{ color }"
    >
      {{ value }}
    </div>

    <div class="text-xs text-slate-500">
      {{ label }}
    </div>
  </div>
</template>
