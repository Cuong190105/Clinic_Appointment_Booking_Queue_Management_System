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
const isSubmitting = ref(false)
const submitted = ref(false)
const error = ref('')
const serverMessage = ref('')

function validateInput() {
  const trimmed = accountInput.value.trim()
  const res = validators.required(trimmed, 'Email hoặc Số điện thoại')
  if (!res.isValid) {
    error.value = res.message
    return false
  }

  // Check if email format or phone format
  const isEmail = trimmed.includes('@')
  if (isEmail) {
    const emailRes = validators.email(trimmed)
    if (!emailRes.isValid) {
      error.value = emailRes.message
      return false
    }
  } else {
    const phoneRes = validators.phone(trimmed)
    if (!phoneRes.isValid) {
      error.value = 'Vui lòng nhập Email hoặc Số điện thoại hợp lệ (10 chữ số).'
      return false
    }
  }

  error.value = ''
  return true
}

async function handleSubmit() {
  if (!validateInput()) return

  isSubmitting.value = true
  error.value = ''

  try {
    const res = await authApi.forgotPassword(accountInput.value.trim())
    serverMessage.value =
      res?.message ||
      'Nếu tài khoản tồn tại trong hệ thống, mật khẩu tạm thời đã được tạo và gửi đến Email / Số điện thoại đăng ký của bạn.'
    submitted.value = true
  } catch (err: any) {
    error.value =
      err?.response?.data?.message ||
      err?.message ||
      'Không thể gửi yêu cầu đặt lại mật khẩu. Vui lòng thử lại sau.'
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex items-center justify-center px-4 py-10 font-sans">
    <div class="w-full max-w-md rounded-3xl border border-slate-200 bg-white p-6 sm:p-8 shadow-sm">
      <div class="mb-6">
        <button
          type="button"
          class="mb-4 inline-flex items-center gap-1.5 text-sm font-medium text-[#0E4D92] hover:underline cursor-pointer focus:outline-none"
          @click="router.push('/login')"
        >
          ← Quay lại đăng nhập
        </button>
        <h1 class="text-2xl font-bold text-slate-800">Quên mật khẩu</h1>
        <p class="mt-2 text-sm text-slate-500">
          Nhập email hoặc số điện thoại đã đăng ký để nhận mật khẩu tạm thời.
        </p>
      </div>

      <!-- Banner thông báo lỗi nếu có -->
      <div v-if="error" class="mb-4">
        <BaseAlert type="error" :message="error" />
      </div>

      <form v-if="!submitted" @submit.prevent="handleSubmit" class="space-y-4" novalidate>
        <BaseInput
          v-model="accountInput"
          label="Email hoặc Số điện thoại"
          required
          placeholder="email@example.com hoặc 0912345678"
          :error="error ? '' : undefined"
          autocomplete="username"
        />

        <div class="pt-2">
          <BaseButton
            type="submit"
            variant="primary"
            size="lg"
            block
            :loading="isSubmitting"
            loading-text="Đang gửi yêu cầu…"
          >
            Gửi yêu cầu đặt lại mật khẩu
          </BaseButton>
        </div>
      </form>

      <!-- Màn hình thông báo thành công sau khi gửi -->
      <div v-else class="space-y-4">
        <BaseAlert
          type="success"
          title="Yêu cầu đã được xử lý!"
        >
          <p class="mt-1 leading-relaxed">
            {{ serverMessage }}
          </p>
          <p class="mt-2 text-xs text-emerald-700">
            Vui lòng kiểm tra hộp thư email (bao gồm cả thư mục Spam) hoặc tin nhắn điện thoại để lấy mật khẩu tạm thời và đăng nhập.
          </p>
        </BaseAlert>

        <BaseButton
          type="button"
          variant="outline"
          size="md"
          block
          @click="router.push('/login')"
        >
          Đi tới trang đăng nhập
        </BaseButton>
      </div>
    </div>
  </div>
</template>
