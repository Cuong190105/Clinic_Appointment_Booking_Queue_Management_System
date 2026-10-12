<script setup lang="ts">
import { useId } from 'vue'

const props = withDefaults(
  defineProps<{
    label?: string
    error?: string
    type?: string
    placeholder?: string
    required?: boolean
    disabled?: boolean
    readonly?: boolean
    hint?: string
    name?: string
    autocomplete?: string
    inputClass?: string
  }>(),
  {
    type: 'text',
    required: false,
    disabled: false,
    readonly: false,
  },
)

const emit = defineEmits<{
  blur: [event: FocusEvent]
  focus: [event: FocusEvent]
}>()

const model = defineModel<string | number>({ default: '' })
const autoId = useId()
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
      <input
        :id="autoId"
        v-model="model"
        :type="type"
        :placeholder="placeholder"
        :required="required"
        :disabled="disabled"
        :readonly="readonly"
        :name="name"
        :autocomplete="autocomplete"
        :aria-invalid="!!error"
        @blur="emit('blur', $event)"
        @focus="emit('focus', $event)"
        :class="[
          'w-full border rounded-xl px-3.5 py-2.5 text-sm bg-white text-slate-800 placeholder:text-slate-400 focus:outline-none transition-all disabled:bg-slate-50 disabled:text-slate-400 disabled:cursor-not-allowed',
          error
            ? 'border-red-500 bg-red-50/20 focus:ring-2 focus:ring-red-500'
            : 'border-slate-200 focus:ring-2 focus:ring-[#0E4D92]',
          inputClass,
        ]"
      />
      <slot name="suffix" />
    </div>

    <p v-if="error" class="text-xs text-red-500 mt-0.5 flex items-center gap-1">
      <span>⚠️</span>
      <span>{{ error }}</span>
    </p>
    <p v-else-if="hint" class="text-xs text-slate-400 mt-0.5">{{ hint }}</p>
  </div>
</template>
