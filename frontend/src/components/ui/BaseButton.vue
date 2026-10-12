<script setup lang="ts">
import { computed } from 'vue'

type Variant = 'primary' | 'secondary' | 'outline' | 'danger' | 'ghost'
type Size = 'sm' | 'md' | 'lg'

const props = withDefaults(
  defineProps<{
    variant?: Variant
    size?: Size
    loading?: boolean
    loadingText?: string
    type?: 'button' | 'submit' | 'reset'
    disabled?: boolean
    block?: boolean
  }>(),
  {
    variant: 'primary',
    size: 'md',
    type: 'button',
    loading: false,
    disabled: false,
    block: false,
  },
)

const emit = defineEmits<{
  click: [event: MouseEvent]
}>()

const variants: Record<Variant, string> = {
  primary:
    'bg-[#0E4D92] text-white hover:bg-[#0b3d75] active:bg-[#082e59] focus-visible:ring-[#0E4D92] shadow-sm',
  secondary:
    'bg-slate-100 text-slate-700 hover:bg-slate-200 active:bg-slate-300 focus-visible:ring-slate-400',
  outline:
    'border border-slate-200 bg-white text-slate-700 hover:bg-slate-50 active:bg-slate-100 focus-visible:ring-slate-300',
  danger:
    'bg-red-600 text-white hover:bg-red-700 active:bg-red-800 focus-visible:ring-red-500 shadow-sm',
  ghost:
    'bg-transparent text-slate-700 hover:bg-slate-100 active:bg-slate-200 focus-visible:ring-slate-300',
}

const sizes: Record<Size, string> = {
  sm: 'h-9 px-3.5 text-xs rounded-lg',
  md: 'h-11 px-4 py-2.5 text-sm rounded-xl',
  lg: 'h-12 px-6 py-3 text-base rounded-xl font-semibold',
}

const classes = computed(() => [
  variants[props.variant],
  sizes[props.size],
  props.block ? 'w-full' : '',
])
</script>

<template>
  <button
    :type="type"
    :disabled="disabled || loading"
    @click="emit('click', $event)"
    class="inline-flex items-center justify-center gap-2 font-medium transition-all focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:outline-none select-none cursor-pointer active:scale-[0.98] disabled:cursor-not-allowed disabled:opacity-50 disabled:active:scale-100"
    :class="classes"
  >
    <span
      v-if="loading"
      class="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent shrink-0"
      role="status"
      aria-label="Đang tải"
    />
    <span v-if="loading && loadingText">{{ loadingText }}</span>
    <slot v-else />
  </button>
</template>
