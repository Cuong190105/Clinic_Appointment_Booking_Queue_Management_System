<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  status: string
}>()

const badgeClass = computed(() => {
  switch (props.status.toLowerCase()) {
    case 'active':
    case 'confirmed':
    case 'completed':
      return 'bg-green-100 text-green-700'

    case 'pending':
      return 'bg-yellow-100 text-yellow-700'

    case 'inactive':
      return 'bg-gray-100 text-gray-700'

    case 'cancelled':
    case 'rejected':
      return 'bg-red-100 text-red-700'

    default:
      return 'bg-blue-100 text-blue-700'
  }
})
</script>

<template>
  <span
    class="inline-flex items-center rounded-full px-3 py-1 text-sm font-medium"
    :class="badgeClass"
  >
    {{ status }}
  </span>
</template>
