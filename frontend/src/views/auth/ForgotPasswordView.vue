<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { authApi } from '@/features/auth/auth.api'
import { validators } from '@/utils/validators'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

const router = useRouter()
const accountInput = ref('')
const resetCode = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const isSubmitting = ref(false)
const codeRequested = ref(false)
const completed = ref(false)
const error = ref('')
const serverMessage = ref('')

function validateAccount() {
  const trimmed = accountInput.value.trim()
  const result = validators.required(trimmed, 'Email hoặc số điện thoại')
  if (!result.isValid) {
    error.value = result.message
    return false
  }
  if (trimmed.includes('@') && !validators.email(trimmed).isValid) {
    error.value = 'Vui lòng nhập email hợp lệ.'
    return false
  }
  if (!trimmed.includes('@') && !validators.phone(trimmed).isValid) {
    error.value = 'Vui lòng nhập email hoặc số điện thoại hợp lệ.'
    return false
  }
  error.value = ''
  return true
}

async function requestCode() {
  if (!validateAccount()) return
  isSubmitting.value = true
  error.value = ''
  try {
    const response = await authApi.forgotPassword(accountInput.value.trim())
    serverMessage.value = response?.message || 'Nếu tài khoản tồn tại, mã đặt lại đã được gửi đến thông tin liên hệ đã đăng ký.'
    codeRequested.value = true
  } catch (err: any) {
    error.value = err?.response?.data?.message || err?.message || 'Không thể gửi yêu cầu. Vui lòng thử lại sau.'
  } finally {
    isSubmitting.value = false
  }
}

async function resetPassword() {
  error.value = ''
  if (!resetCode.value.trim()) {
    error.value = 'Vui lòng nhập mã đặt lại mật khẩu.'
    return
  }
  if (newPassword.value.length < 8) {
    error.value = 'Mật khẩu mới phải có ít nhất 8 ký tự.'
    return
  }
  if (newPassword.value !== confirmPassword.value) {
    error.value = 'Mật khẩu xác nhận không khớp.'
    return
  }

  isSubmitting.value = true
  try {
    const response = await authApi.resetPassword({ token: resetCode.value, newPassword: newPassword.value })
    serverMessage.value = response?.message || 'Nếu mã hợp lệ, mật khẩu đã được cập nhật.'
    completed.value = true
  } catch (err: any) {
    error.value = err?.response?.data?.message || err?.message || 'Không thể đặt lại mật khẩu. Vui lòng thử lại.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex items-center justify-center px-4 py-10 font-sans">
    <div class="w-full max-w-md rounded-3xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
      <div class="mb-6">
        <button type="button" class="mb-4 text-sm font-medium text-[#0E4D92] hover:underline" @click="router.push('/login')">
          ← Quay lại đăng nhập
        </button>
        <h1 class="text-2xl font-bold text-slate-800">Quên mật khẩu</h1>
        <p class="mt-2 text-sm text-slate-500">
          {{ codeRequested ? 'Nhập mã đã nhận và đặt mật khẩu mới.' : 'Nhập email hoặc số điện thoại đã đăng ký để nhận mã đặt lại.' }}
        </p>
      </div>

      <div v-if="error" class="mb-4"><BaseAlert type="error" :message="error" /></div>

      <form v-if="!codeRequested" class="space-y-4" novalidate @submit.prevent="requestCode">
        <BaseInput v-model="accountInput" label="Email hoặc số điện thoại" required autocomplete="username" />
        <BaseButton type="submit" variant="primary" size="lg" block :loading="isSubmitting" loading-text="Đang gửi…">
          Gửi mã đặt lại
        </BaseButton>
      </form>

      <form v-else-if="!completed" class="space-y-4" novalidate @submit.prevent="resetPassword">
        <BaseAlert type="success" :message="serverMessage" />
        <BaseInput v-model="resetCode" label="Mã đặt lại" required autocomplete="one-time-code" />
        <BaseInput v-model="newPassword" label="Mật khẩu mới" type="password" required autocomplete="new-password" />
        <BaseInput v-model="confirmPassword" label="Xác nhận mật khẩu mới" type="password" required autocomplete="new-password" />
        <BaseButton type="submit" variant="primary" size="lg" block :loading="isSubmitting" loading-text="Đang cập nhật…">
          Đặt lại mật khẩu
        </BaseButton>
      </form>

      <div v-else class="space-y-4">
        <BaseAlert type="success" :message="serverMessage" />
        <BaseButton type="button" variant="outline" size="md" block @click="router.push('/login')">
          Đi tới đăng nhập
        </BaseButton>
      </div>
    </div>
  </div>
</template>
