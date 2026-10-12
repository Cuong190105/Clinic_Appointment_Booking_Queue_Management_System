<script setup lang="ts">
import BaseButton from '@/components/ui/BaseButton.vue'

withDefaults(
  defineProps<{
    searchPlaceholder?: string
    addButtonLabel?: string
    showAddButton?: boolean
    showResetButton?: boolean
  }>(),
  {
    searchPlaceholder: 'Tìm kiếm…',
    addButtonLabel: '+ Thêm mới',
    showAddButton: true,
    showResetButton: true,
  },
)

const emit = defineEmits<{
  reset: []
  add: []
}>()

const search = defineModel<string>('search', { default: '' })
</script>

<template>
  <div class="rounded-2xl border border-slate-200 bg-white p-4 sm:p-5 shadow-2xs font-sans">
    <div class="flex flex-col gap-3 lg:flex-row lg:items-center justify-between">
      <div class="flex flex-1 flex-col sm:flex-row items-stretch sm:items-center gap-3">
        <!-- Search input -->
        <div class="relative flex-1 min-w-[220px]">
          <input
            v-model="search"
            type="text"
            :placeholder="searchPlaceholder"
            class="w-full rounded-xl border border-slate-200 bg-slate-50/50 px-3.5 py-2.5 pl-10 text-sm text-slate-800 placeholder:text-slate-400 focus:border-[#0E4D92] focus:bg-white focus:outline-none transition-colors"
          />
          <svg
            class="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <circle cx="11" cy="11" r="8" />
            <path d="m21 21-4.3-4.3" />
          </svg>
        </div>

        <!-- Custom Filters Slot (e.g. Specialty, Status, Role dropdowns) -->
        <div v-if="$slots.filters" class="flex flex-wrap items-center gap-2.5">
          <slot name="filters" />
        </div>

        <!-- Reset Button -->
        <BaseButton
          v-if="showResetButton"
          type="button"
          variant="outline"
          size="md"
          @click="emit('reset')"
        >
          Đặt lại
        </BaseButton>
      </div>

      <!-- Action Button (Add new) -->
      <div v-if="showAddButton || $slots.actions" class="flex items-center gap-2.5 shrink-0">
        <slot name="actions">
          <BaseButton
            v-if="showAddButton"
            type="button"
            variant="primary"
            size="md"
            @click="emit('add')"
          >
            {{ addButtonLabel }}
          </BaseButton>
        </slot>
      </div>
    </div>
  </div>
</template>
