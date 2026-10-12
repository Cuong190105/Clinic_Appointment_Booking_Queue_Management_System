<script setup lang="ts">
import { computed, useId } from 'vue'

interface SelectOption {
  label: string
  value: string | number
}

const props = withDefaults(
  defineProps<{
    label?: string
    error?: string
    required?: boolean
    disabled?: boolean
    options?: (string | SelectOption)[]
    placeholder?: string
    hint?: string
    inputClass?: string
  }>(),
  {
    required: false,
    disabled: false,
    options: () => [],
  },
)

const emit = defineEmits<{
  blur: [event: FocusEvent]
  change: [event: Event]
}>()

const model = defineModel<string | number>({ default: '' })
const autoId = useId()

const normalizedOptions = computed<SelectOption[]>(() => {
  return props.options.map((opt) => {
    if (typeof opt === 'object' && opt !== null) {
      return opt
    }
    return { label: String(opt), value: opt }
  })
})
</script>

<template>
  <div class="flex flex-col gap-1 w-full">
    <label
      v-if="label"
      :for="autoId"
      class="block text-sm font-medium text-slate-700 select-none"
    >
      {{ label }}
      <span v-if="required" class="text-red-500 font-semibold">*</span>
    </label>

    <div class="relative w-full">
      <select
        :id="autoId"
        v-model="model"
        :required="required"
        :disabled="disabled"
        :aria-invalid="!!error"
        @blur="emit('blur', $event)"
        @change="emit('change', $event)"
        :class="[
          'w-full border rounded-xl px-3.5 py-2.5 text-sm bg-white text-slate-800 focus:outline-none transition-all appearance-none cursor-pointer disabled:bg-slate-50 disabled:text-slate-400 disabled:cursor-not-allowed',
          error
            ? 'border-red-500 bg-red-50/20 focus:ring-2 focus:ring-red-500'
            : 'border-slate-200 focus:ring-2 focus:ring-[#0E4D92]',
          inputClass,
        ]"
      >
        <option v-if="placeholder" value="" disabled selected>{{ placeholder }}</option>
        <slot>
          <option
            v-for="opt in normalizedOptions"
            :key="opt.value"
            :value="opt.value"
          >
            {{ opt.label }}
          </option>
        </slot>
      </select>

      <!-- Custom Chevron Icon -->
      <div class="pointer-events-none absolute right-3.5 top-1/2 -translate-y-1/2 text-slate-400">
        <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </div>
    </div>

    <p v-if="error" class="text-xs text-red-500 mt-0.5 flex items-center gap-1">
      <span>⚠️</span>
      <span>{{ error }}</span>
    </p>
    <p v-else-if="hint" class="text-xs text-slate-400 mt-0.5">{{ hint }}</p>
  </div>
</template>
