<script setup lang="ts">
import type { AvailableSlot } from '../patient.types'

const props = defineProps<{
  slots: AvailableSlot[]
  selectedTime?: string
}>()

const emit = defineEmits<{
  selectSlot: [slot: AvailableSlot]
}>()
</script>

<template>
  <div class="mt-4 pt-3.5 border-t border-slate-100">
    <p class="mb-2 text-xs font-bold text-slate-700 uppercase tracking-wider">
      Chọn khung giờ khám:
    </p>

    <div v-if="!slots.length" class="text-xs text-slate-400 italic py-1">
      Chưa có khung giờ khám khả dụng cho ngày đã chọn.
    </div>

    <div v-else class="flex flex-wrap gap-2">
      <button
        v-for="slot in slots"
        :key="slot.id"
        type="button"
        :disabled="!slot.available"
        class="rounded-xl border px-3.5 py-1.5 text-xs font-semibold transition-all select-none"
        :class="
          !slot.available
            ? 'cursor-not-allowed border-slate-200 bg-slate-100 text-slate-400 line-through'
            : selectedTime === slot.time
              ? 'border-[#0E4D92] bg-[#0E4D92] text-white shadow-xs'
              : 'border-slate-200 bg-white text-slate-800 hover:border-[#0E4D92] hover:text-[#0E4D92] active:bg-slate-50'
        "
        @click.stop="emit('selectSlot', slot)"
      >
        {{ slot.time }}
      </button>
    </div>
  </div>
</template>
