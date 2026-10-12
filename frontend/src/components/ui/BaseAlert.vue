<script setup lang="ts">
import { computed } from 'vue'

type AlertType = 'error' | 'success' | 'warning' | 'info'

const props = withDefaults(
  defineProps<{
    type?: AlertType
    title?: string
    message?: string
    dismissible?: boolean
  }>(),
  {
    type: 'error',
    dismissible: false,
  },
)

const emit = defineEmits<{
  dismiss: []
}>()

const styles: Record<AlertType, { container: string; icon: string; text: string }> = {
  error: {
    container: 'bg-red-50 border-red-200 text-red-700',
    icon: 'text-red-500',
    text: 'text-red-700',
  },
  success: {
    container: 'bg-emerald-50 border-emerald-200 text-emerald-800',
    icon: 'text-emerald-500',
    text: 'text-emerald-800',
  },
  warning: {
    container: 'bg-amber-50 border-amber-200 text-amber-800',
    icon: 'text-amber-500',
    text: 'text-amber-800',
  },
  info: {
    container: 'bg-blue-50 border-blue-200 text-blue-800',
    icon: 'text-blue-500',
    text: 'text-blue-800',
  },
}

const currentStyle = computed(() => styles[props.type])
</script>

<template>
  <div
    :class="[
      'flex items-start gap-3 p-3.5 rounded-xl border text-sm transition-all duration-200',
      currentStyle.container,
    ]"
    role="alert"
  >
    <!-- Icon -->
    <div :class="['shrink-0 mt-0.5', currentStyle.icon]">
      <svg
        v-if="type === 'error'"
        xmlns="http://www.w3.org/2000/svg"
        class="w-4 h-4"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <circle cx="12" cy="12" r="10" />
        <line x1="12" y1="8" x2="12" y2="12" />
        <line x1="12" y1="16" x2="12.01" y2="16" />
      </svg>
      <svg
        v-else-if="type === 'success'"
        xmlns="http://www.w3.org/2000/svg"
        class="w-4 h-4"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
        <polyline points="22 4 12 14.01 9 11.01" />
      </svg>
      <svg
        v-else-if="type === 'warning'"
        xmlns="http://www.w3.org/2000/svg"
        class="w-4 h-4"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z" />
        <line x1="12" y1="9" x2="12" y2="13" />
        <line x1="12" y1="17" x2="12.01" y2="17" />
      </svg>
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
        <circle cx="12" cy="12" r="10" />
        <line x1="12" y1="16" x2="12" y2="12" />
        <line x1="12" y1="8" x2="12.01" y2="8" />
      </svg>
    </div>

    <!-- Content -->
    <div class="flex-1 text-xs sm:text-sm">
      <p v-if="title" class="font-semibold mb-0.5">{{ title }}</p>
      <slot>
        <p v-if="message">{{ message }}</p>
      </slot>
    </div>

    <!-- Dismiss Button -->
    <button
      v-if="dismissible"
      type="button"
      @click="emit('dismiss')"
      class="shrink-0 text-slate-400 hover:text-slate-600 p-0.5 rounded-lg focus:outline-none transition-colors"
      aria-label="Đóng thông báo"
    >
      <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <line x1="18" y1="6" x2="6" y2="18" />
        <line x1="6" y1="6" x2="18" y2="18" />
      </svg>
    </button>
  </div>
</template>
