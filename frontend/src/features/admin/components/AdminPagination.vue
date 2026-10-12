<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    currentPage: number
    totalPages: number
    totalCount?: number
  }>(),
  {
    totalCount: undefined,
  },
)

const emit = defineEmits<{
  'update:currentPage': [page: number]
}>()

function goToPage(page: number) {
  if (page < 1 || page > props.totalPages || page === props.currentPage) {
    return
  }
  emit('update:currentPage', page)
}

const visiblePages = computed(() => {
  const current = props.currentPage
  const total = props.totalPages
  if (total <= 7) {
    return Array.from({ length: total }, (_, i) => i + 1)
  }

  const pages: (number | string)[] = []
  pages.push(1)

  if (current > 3) {
    pages.push('...')
  }

  const start = Math.max(2, current - 1)
  const end = Math.min(total - 1, current + 1)

  for (let i = start; i <= end; i++) {
    pages.push(i)
  }

  if (current < total - 2) {
    pages.push('...')
  }

  pages.push(total)
  return pages
})
</script>

<template>
  <div class="flex flex-col sm:flex-row items-center justify-between gap-3 pt-4 border-t border-slate-100">
    <p class="text-xs text-slate-500">
      <template v-if="totalCount !== undefined">
        Tổng số <span class="font-semibold text-slate-700">{{ totalCount }}</span> kết quả ·
      </template>
      Trang <span class="font-semibold text-slate-700">{{ currentPage }}</span> / {{ Math.max(1, totalPages) }}
    </p>

    <div class="flex items-center gap-1">
      <button
        type="button"
        class="inline-flex items-center justify-center rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50 transition-colors disabled:cursor-not-allowed disabled:opacity-40 cursor-pointer focus:outline-none"
        :disabled="currentPage <= 1"
        @click="goToPage(currentPage - 1)"
      >
        Trước
      </button>

      <template v-for="(page, idx) in visiblePages" :key="idx">
        <span
          v-if="page === '...'"
          class="px-2 text-xs text-slate-400 select-none"
        >
          …
        </span>
        <button
          v-else
          type="button"
          class="h-8 min-w-8 rounded-lg px-2 text-xs font-medium transition-colors cursor-pointer focus:outline-none"
          :class="
            page === currentPage
              ? 'bg-[#0E4D92] text-white shadow-xs'
              : 'text-slate-600 hover:bg-slate-100'
          "
          @click="goToPage(Number(page))"
        >
          {{ page }}
        </button>
      </template>

      <button
        type="button"
        class="inline-flex items-center justify-center rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-50 transition-colors disabled:cursor-not-allowed disabled:opacity-40 cursor-pointer focus:outline-none"
        :disabled="currentPage >= totalPages || totalPages === 0"
        @click="goToPage(currentPage + 1)"
      >
        Sau
      </button>
    </div>
  </div>
</template>
