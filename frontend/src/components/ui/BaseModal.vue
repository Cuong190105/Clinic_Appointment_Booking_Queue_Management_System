<script setup lang="ts">
import { onMounted, onUnmounted, watch } from 'vue'

const props = withDefaults(
  defineProps<{
    open: boolean
    title?: string
    subtitle?: string
    icon?: string
    size?: 'sm' | 'md' | 'lg' | 'xl' | '2xl' | '3xl' | '4xl'
    hideCloseButton?: boolean
  }>(),
  {
    title: '',
    subtitle: '',
    icon: '',
    size: 'md',
    hideCloseButton: false,
  },
)

const emit = defineEmits<{
  close: []
}>()

const sizeClasses: Record<string, string> = {
  sm: 'max-w-sm',
  md: 'max-w-md',
  lg: 'max-w-lg',
  xl: 'max-w-xl',
  '2xl': 'max-w-2xl',
  '3xl': 'max-w-3xl',
  '4xl': 'max-w-4xl',
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && props.open) {
    emit('close')
  }
}

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      document.body.style.overflow = 'hidden'
    } else {
      document.body.style.overflow = ''
    }
  },
)

onMounted(() => {
  window.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
  document.body.style.overflow = ''
})
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-6 bg-slate-900/50 backdrop-blur-xs font-sans overflow-y-auto"
      role="dialog"
      aria-modal="true"
      @click.self="emit('close')"
    >
      <div
        class="w-full bg-white rounded-2xl shadow-2xl border border-slate-200 overflow-hidden transform transition-all my-auto animate-in fade-in zoom-in-95 duration-200 flex flex-col max-h-[90vh]"
        :class="sizeClasses[size]"
      >
        <!-- Modal Header -->
        <div
          v-if="title || $slots.header"
          class="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-slate-50/70 shrink-0"
        >
          <slot name="header">
            <div class="flex items-center gap-3">
              <div
                v-if="icon"
                class="w-9 h-9 rounded-xl bg-blue-50 text-[#0E4D92] flex items-center justify-center font-bold text-base shrink-0"
              >
                {{ icon }}
              </div>
              <div>
                <h2 class="text-base font-bold text-slate-800">{{ title }}</h2>
                <p v-if="subtitle" class="text-xs text-slate-500 mt-0.5">{{ subtitle }}</p>
              </div>
            </div>
          </slot>

          <button
            v-if="!hideCloseButton"
            type="button"
            @click="emit('close')"
            class="text-slate-400 hover:text-slate-600 hover:bg-slate-100 p-1.5 rounded-lg focus:outline-none transition-colors cursor-pointer"
            aria-label="Đóng"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>

        <!-- Modal Body -->
        <div class="p-6 overflow-y-auto flex-1">
          <slot />
        </div>

        <!-- Modal Footer -->
        <div
          v-if="$slots.footer"
          class="flex items-center justify-end gap-3 px-6 py-4 border-t border-slate-100 bg-slate-50/70 shrink-0"
        >
          <slot name="footer" />
        </div>
      </div>
    </div>
  </Teleport>
</template>
