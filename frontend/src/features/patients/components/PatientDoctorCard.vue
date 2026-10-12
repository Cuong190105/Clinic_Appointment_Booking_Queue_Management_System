<script setup lang="ts">
import type { Doctor } from '../patient.types'
import { formatSpecialtyName } from '@/features/specialties/specialties.utils'

const props = defineProps<{
  doctor: Doctor
  selected?: boolean
}>()

const emit = defineEmits<{
  select: [id: string | number]
}>()
</script>

<template>
  <div
    class="cursor-pointer rounded-2xl border-2 bg-white p-4 transition-all duration-150 select-none hover:shadow-xs"
    :class="
      selected
        ? 'border-[#0E4D92] bg-blue-50/20 shadow-xs'
        : 'border-slate-200 hover:border-slate-300'
    "
    @click="emit('select', doctor.id)"
  >
    <div class="flex items-center gap-3.5">
      <div
        class="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl font-bold transition-colors"
        :class="selected ? 'bg-[#0E4D92] text-white' : 'bg-blue-50 text-[#0E4D92]'"
      >
        BS
      </div>

      <div class="min-w-0 flex-1">
        <p class="font-bold text-slate-800 truncate text-sm sm:text-base">
          {{ doctor.name }}
        </p>

        <p class="text-xs text-slate-500 truncate mt-0.5">
          {{ formatSpecialtyName(doctor.specialty) }}
          <span v-if="doctor.room" class="text-slate-400"> · {{ doctor.room }}</span>
        </p>
      </div>

      <div class="shrink-0">
        <div
          class="h-6 w-6 rounded-full flex items-center justify-center border transition-all"
          :class="
            selected
              ? 'bg-[#0E4D92] border-[#0E4D92] text-white'
              : 'border-slate-300 bg-white'
          "
        >
          <svg v-if="selected" xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="20 6 9 17 4 12" />
          </svg>
        </div>
      </div>
    </div>

    <!-- Slot selection container if slot is embedded -->
    <slot />
  </div>
</template>
