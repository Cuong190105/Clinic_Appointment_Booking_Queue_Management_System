<script setup lang="ts">
defineProps<{
  label: string
  value: string
  sub: string
}>()
</script>

<template>
  <div class="rounded-xl border border-slate-200 bg-white p-5">
    <div class="mb-0.5 text-2xl font-bold text-slate-800">
      {{ value }}
    </div>

    <div class="mb-1 text-sm text-slate-500">
      {{ label }}
    </div>

    <div class="text-xs text-slate-400">
      {{ sub }}
    </div>
  </div>
</template>
