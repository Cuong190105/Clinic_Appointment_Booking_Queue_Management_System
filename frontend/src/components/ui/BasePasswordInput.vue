<script setup lang="ts">
import { ref, useId } from 'vue'

withDefaults(
  defineProps<{
    label?: string
    error?: string
    placeholder?: string
    required?: boolean
    disabled?: boolean
    hint?: string
    name?: string
    autocomplete?: string
    inputClass?: string
  }>(),
  {
    placeholder: '••••••••',
    required: false,
    disabled: false,
  },
)

const emit = defineEmits<{
  blur: [event: FocusEvent]
  focus: [event: FocusEvent]
}>()

const model = defineModel<string>({ default: '' })
const showPassword = ref(false)
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
        :type="showPassword ? 'text' : 'password'"
        :placeholder="placeholder"
        :required="required"
        :disabled="disabled"
        :name="name"
        :autocomplete="autocomplete"
        :aria-invalid="!!error"
        @blur="emit('blur', $event)"
        @focus="emit('focus', $event)"
        :class="[
          'w-full border rounded-xl pl-3.5 pr-11 py-2.5 text-sm bg-white text-slate-800 placeholder:text-slate-400 focus:outline-none transition-all disabled:bg-slate-50 disabled:text-slate-400 disabled:cursor-not-allowed',
          error
            ? 'border-red-500 bg-red-50/20 focus:ring-2 focus:ring-red-500'
            : 'border-slate-200 focus:ring-2 focus:ring-[#0E4D92]',
          inputClass,
        ]"
      />

      <button
        type="button"
        @click="showPassword = !showPassword"
        tabindex="-1"
        class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 focus:outline-none p-1 rounded-lg transition-colors cursor-pointer"
        :title="showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'"
        :aria-label="showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'"
      >
        <!-- Eye Off Icon -->
        <svg
          v-if="showPassword"
          xmlns="http://www.w3.org/2000/svg"
          class="w-4 h-4"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <path d="M9.88 9.88a3 3 0 1 0 4.24 4.24" />
          <path d="M10.73 5.08A10.43 10.43 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68" />
          <path d="M6.61 6.61A13.526 13.526 0 0 0 2 12s3 7 10 7a9.74 9.74 0 0 0 5.39-1.61" />
          <line x1="2" y1="2" x2="22" y2="22" />
        </svg>

        <!-- Eye Icon -->
        <svg
          v-else
          xmlns="http://www.w3.org/2000/svg"
          class="w-4 h-4"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z" />
          <circle cx="12" cy="12" r="3" />
        </svg>
      </button>
    </div>

    <p v-if="error" class="text-xs text-red-500 mt-0.5 flex items-center gap-1">
      <span>⚠️</span>
      <span>{{ error }}</span>
    </p>
    <p v-else-if="hint" class="text-xs text-slate-400 mt-0.5">{{ hint }}</p>
  </div>
</template>
