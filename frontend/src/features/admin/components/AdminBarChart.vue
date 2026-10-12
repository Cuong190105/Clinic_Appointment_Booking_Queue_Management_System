<script setup lang="ts">
defineProps<{
  data: {
    day: string
    value: number
  }[]
  maxVal: number
}>()
</script>

<template>
  <div class="flex h-56 items-end gap-3">
    <div
      v-for="item in data"
      :key="item.day"
      class="flex h-full flex-1 flex-col items-center justify-end gap-2"
    >
      <span class="text-[11px] text-slate-400">
        {{ item.value }}
      </span>

      <div
        class="w-full max-w-10 rounded-t-md bg-[#0E4D92] transition-all"
        :style="{
          height: `${(item.value / maxVal) * 100}%`,
        }"
      />

      <span class="text-xs text-slate-400">
        {{ item.day }}
      </span>
    </div>
  </div>
</template>
