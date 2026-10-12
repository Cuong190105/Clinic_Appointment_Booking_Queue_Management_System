<script setup lang="ts">
import { ref, watch } from 'vue'
import { authApi } from '@/features/auth/auth.api'
import { validators } from '@/utils/validators'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BasePasswordInput from '@/components/ui/BasePasswordInput.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  close: []
  success: []
}>()

const currentPassword = ref('')
const newPassword = ref('')
const confirmNewPassword = ref('')

const isSubmitting = ref(false)
const error = ref('')
const successMessage = ref('')

const fieldErrors = ref({
  currentPassword: '',
  newPassword: '',
  confirmNewPassword: '',
})

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      resetForm()
    }
  },
)

function resetForm() {
  currentPassword.value = ''
  newPassword.value = ''
  confirmNewPassword.value = ''
  error.value = ''
  successMessage.value = ''
  fieldErrors.value = {
    currentPassword: '',
    newPassword: '',
    confirmNewPassword: '',
  }
}

function validateForm(): boolean {
  fieldErrors.value.currentPassword = ''
  fieldErrors.value.newPassword = ''
  fieldErrors.value.confirmNewPassword = ''
  error.value = ''

  const currReq = validators.required(currentPassword.value, 'Mật khẩu hiện tại')
  if (!currReq.isValid) {
    fieldErrors.value.currentPassword = currReq.message
  } else if (currentPassword.value.length < 8) {
    fieldErrors.value.currentPassword = 'Mật khẩu hiện tại phải có ít nhất 8 ký tự.'
  }

  const newReq = validators.required(newPassword.value, 'Mật khẩu mới')
  if (!newReq.isValid) {
    fieldErrors.value.newPassword = newReq.message
  } else if (newPassword.value.length < 8) {
    fieldErrors.value.newPassword = 'Mật khẩu mới phải có ít nhất 8 ký tự.'
  } else if (newPassword.value === currentPassword.value) {
    fieldErrors.value.newPassword = 'Mật khẩu mới không được trùng với mật khẩu hiện tại.'
  }

  const confirmRes = validators.confirmPassword(newPassword.value, confirmNewPassword.value)
  if (!confirmRes.isValid) {
    fieldErrors.value.confirmNewPassword = confirmRes.message
  }

  return (
    !fieldErrors.value.currentPassword &&
    !fieldErrors.value.newPassword &&
    !fieldErrors.value.confirmNewPassword
  )
}

async function handleChangePassword() {
  if (!validateForm()) return

  isSubmitting.value = true
  error.value = ''
  successMessage.value = ''

  try {
    const res = await authApi.changePassword({
      currentPassword: currentPassword.value,
      newPassword: newPassword.value,
    })

    successMessage.value = res?.message || 'Mật khẩu đã được cập nhật thành công!'
    emit('success')

    setTimeout(() => {
      if (props.open) {
        emit('close')
      }
    }, 2000)
  } catch (err: any) {
    error.value =
      err?.response?.data?.message ||
      err?.response?.data?.errorMessages?.[0] ||
      err?.message ||
      'Không thể đổi mật khẩu. Vui lòng kiểm tra lại mật khẩu hiện tại.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xs font-sans"
  >
    <div
      class="w-full max-w-md bg-white rounded-2xl shadow-xl border border-slate-200 overflow-hidden transform transition-all"
      role="dialog"
      aria-modal="true"
    >
      <!-- Modal Header -->
      <div class="flex items-center justify-between px-6 py-4 border-b border-slate-100">
        <div class="flex items-center gap-2.5">
          <div class="w-8 h-8 rounded-lg bg-blue-50 text-[#0E4D92] flex items-center justify-center font-bold">
            🔒
          </div>
          <h2 class="text-lg font-bold text-slate-800">Đổi mật khẩu</h2>
        </div>
        <button
          type="button"
          @click="emit('close')"
          class="text-slate-400 hover:text-slate-600 p-1.5 rounded-lg focus:outline-none transition-colors"
          aria-label="Đóng"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      </div>

      <!-- Modal Body -->
      <div class="p-6">
        <!-- Thông báo thành công -->
        <div v-if="successMessage" class="mb-4">
          <BaseAlert type="success" :message="successMessage" />
        </div>

        <!-- Thông báo lỗi -->
        <div v-if="error" class="mb-4">
          <BaseAlert type="error" :message="error" />
        </div>

        <form @submit.prevent="handleChangePassword" class="space-y-4" novalidate>
          <BasePasswordInput
            v-model="currentPassword"
            label="Mật khẩu hiện tại"
            required
            placeholder="Nhập mật khẩu đang dùng"
            :error="fieldErrors.currentPassword"
            autocomplete="current-password"
          />

          <BasePasswordInput
            v-model="newPassword"
            label="Mật khẩu mới"
            required
            placeholder="Tối thiểu 8 ký tự"
            :error="fieldErrors.newPassword"
            autocomplete="new-password"
          />

          <BasePasswordInput
            v-model="confirmNewPassword"
            label="Xác nhận mật khẩu mới"
            required
            placeholder="Nhập lại mật khẩu mới"
            :error="fieldErrors.confirmNewPassword"
            autocomplete="new-password"
          />

          <div class="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
            <BaseButton
              type="button"
              variant="outline"
              size="md"
              @click="emit('close')"
              :disabled="isSubmitting"
            >
              Hủy bỏ
            </BaseButton>

            <BaseButton
              type="submit"
              variant="primary"
              size="md"
              :loading="isSubmitting"
              loading-text="Đang cập nhật…"
            >
              Lưu thay đổi
            </BaseButton>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
