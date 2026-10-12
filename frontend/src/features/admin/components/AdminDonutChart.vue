<script setup lang="ts">
defineProps<{
  data: {
    label: string
    value: number
    color: string
  }[]
}>()
</script>

<template>
  <div class="flex items-center gap-8">
    <div class="relative h-40 w-40 shrink-0">
      <div
        class="h-full w-full rounded-full"
        style="
          background: conic-gradient(
            #00A878 0deg 230deg,
            #0E4D92 230deg 318deg,
            #EF4444 318deg 354deg,
            #F59E0B 354deg 360deg
          );
        "
      />

      <div
        class="absolute inset-7 flex items-center justify-center
               rounded-full bg-white"
      >
        <span class="text-lg font-bold text-slate-800">
          {{ data.reduce((sum, item) => sum + item.value, 0) }}
        </span>
      </div>
    </div>

    <div class="space-y-3">
      <div
        v-for="item in data"
        :key="item.label"
        class="flex items-center gap-2"
      >
        <span
          class="h-2.5 w-2.5 rounded-full"
          :style="{ backgroundColor: item.color }"
        />

        <span class="text-xs text-slate-600">
          {{ item.label }}
        </span>

        <span class="ml-2 text-xs font-semibold text-slate-800">
          {{ item.value }}
        </span>
      </div>
    </div>
  </div>
</template>
