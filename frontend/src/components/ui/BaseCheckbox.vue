<script setup lang="ts">
import { useId } from 'vue'

withDefaults(
  defineProps<{
    label?: string
    description?: string
    disabled?: boolean
    required?: boolean
  }>(),
  {
    disabled: false,
    required: false,
  },
)

const emit = defineEmits<{
  change: [value: boolean]
}>()

const model = defineModel<boolean>({ default: false })
const autoId = useId()
</script>

<template>
  <label
    :class="[
      'flex items-start gap-2.5 text-sm select-none cursor-pointer group',
      disabled ? 'cursor-not-allowed opacity-60' : '',
    ]"
  >
    <div class="relative flex items-center justify-center mt-0.5">
      <input
        :id="autoId"
        v-model="model"
        type="checkbox"
        :disabled="disabled"
        :required="required"
        class="w-4 h-4 rounded border-slate-300 text-[#0E4D92] focus:ring-[#0E4D92] focus:ring-offset-0 transition-colors cursor-pointer disabled:cursor-not-allowed accent-[#0E4D92]"
      />
    </div>

    <div class="flex flex-col">
      <span
        v-if="label || $slots.default"
        class="font-medium text-slate-700 group-hover:text-slate-900 transition-colors"
      >
        <slot>{{ label }}</slot>
      </span>
      <span
        v-if="description || $slots.description"
        class="text-xs text-slate-500 leading-normal mt-0.5"
      >
        <slot name="description">{{ description }}</slot>
      </span>
    </div>
  </label>
</template>
