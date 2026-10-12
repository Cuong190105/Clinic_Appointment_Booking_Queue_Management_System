<script setup lang="ts">
import { ref } from 'vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import * as patientApiModule from '../patient.api'

const props = withDefaults(
  defineProps<{
    message?: string
  }>(),
  {
    message: 'Kết nối tới API bệnh nhân đang tạm ngắt để tránh lỗi phát sinh. Bạn có thể nhấn thử kết nối lại.',
  },
)

const emit = defineEmits<{
  reconnected: []
}>()

const reconnecting = ref(false)

function isEndpointsDisabled() {
  try {
    const fn = (patientApiModule as any)?.areProtectedPatientEndpointsDisabled
    return typeof fn === 'function' ? fn() : false
  } catch {
    return false
  }
}

async function handleReconnect() {
  reconnecting.value = true
  try {
    const fn = (patientApiModule as any)?.enableProtectedPatientEndpoints
    if (typeof fn === 'function') {
      fn()
    }
    emit('reconnected')
  } finally {
    reconnecting.value = false
  }
}
</script>

<template>
  <div v-if="isEndpointsDisabled()" class="mb-4">
    <BaseAlert type="warning" title="Thông báo kết nối">
      <div class="space-y-3">
        <p class="text-xs sm:text-sm text-amber-900">{{ message }}</p>
        <div>
          <BaseButton
            size="sm"
            variant="primary"
            :loading="reconnecting"
            loading-text="Đang kết nối..."
            @click="handleReconnect"
          >
            Kết nối lại với máy chủ
          </BaseButton>
        </div>
      </div>
    </BaseAlert>
  </div>
</template>
