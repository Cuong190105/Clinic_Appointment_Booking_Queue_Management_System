<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import type { UserRole } from '@/features/auth/auth.types'
import { validators } from '@/utils/validators'
import { getFieldErrors } from '@/lib/api/error-utils'
import { logger } from '@/lib/logger'
import AuthLayout from '@/components/layout/AuthLayout.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BasePasswordInput from '@/components/ui/BasePasswordInput.vue'
import BaseCheckbox from '@/components/ui/BaseCheckbox.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

// Key để lưu trạng thái "Ghi nhớ đăng nhập" vào localStorage
const REMEMBER_ME_KEY = 'clinic.auth.rememberMe'

function getInitialRememberMe(): boolean {
  try {
    return localStorage.getItem(REMEMBER_ME_KEY) === 'true'
  } catch {
    return false
  }
}

const username = ref('')
const password = ref('')
const rememberMe = ref(getInitialRememberMe())

const errors = ref({
  username: '',
  password: '',
})

watch(rememberMe, (value) => {
  try {
    localStorage.setItem(REMEMBER_ME_KEY, String(value))
  } catch {
    // Bỏ qua lỗi storage
  }
})

function getRoleRoute(role: UserRole) {
  if (role === 'Admin') return '/admin'
  if (role === 'Receptionist') return '/reception/queue'
  if (role === 'Doctor') return '/doctor/examination'
  return '/patient'
}

function validateLogin(): boolean {
  errors.value.username = ''
  errors.value.password = ''

  const usernameRequired = validators.required(username.value, 'Tên đăng nhập')
  if (!usernameRequired.isValid) {
    errors.value.username = usernameRequired.message
  }

  const passwordRequired = validators.required(password.value, 'Mật khẩu')
  if (!passwordRequired.isValid) {
    errors.value.password = passwordRequired.message
  } else {
    const passwordValidation = validators.password(password.value)
    if (!passwordValidation.isValid) {
      errors.value.password = passwordValidation.message
    }
  }

  return !errors.value.username && !errors.value.password
}

async function handleLogin() {
  if (!validateLogin()) return

  try {
    const loggedInUser = await authStore.login({
      username: username.value.trim(),
      password: password.value,
      rememberMe: rememberMe.value,
    })

    const redirectQuery = route.query.redirect as string | undefined
    const availableRoles = loggedInUser?.roles?.length
      ? loggedInUser.roles
      : [loggedInUser?.activeRole ?? loggedInUser?.role ?? 'Patient']

    if (availableRoles.length > 1) {
      router.replace({
        path: '/select-role',
        query: redirectQuery ? { redirect: redirectQuery } : undefined,
      })
      return
    }

    const activeRole = (loggedInUser?.activeRole ??
      loggedInUser?.role ??
      availableRoles[0] ??
      'Patient') as UserRole
    authStore.setActiveRole(activeRole)

    router.replace(redirectQuery || getRoleRoute(activeRole))
  } catch (e: any) {
    let mapped = false
    try {
      const fe = getFieldErrors(e)
      if (fe) {
        errors.value.username = fe.username ? fe.username.join(' ') : errors.value.username
        errors.value.password = fe.password ? fe.password.join(' ') : errors.value.password
        mapped = true
      }
    } catch {
      // Bỏ qua lỗi parse
    }

    if (!mapped) {
      const global =
        authStore.error ||
        (e?.response?.data?.errorMessages && e.response.data.errorMessages.join(' ')) ||
        e?.message
      if (global) {
        errors.value.username = errors.value.username || global
      }
    }

    logger.error('Login failed', e)
  }
}

function goToForgotPassword() {
  router.push('/forgot-password')
}
</script>

<template>
  <AuthLayout
    title="Đăng nhập"
    subtitle="Đăng nhập để sử dụng các chức năng của hệ thống."
    hero-title="Đăng nhập&#10;để tiếp tục 👋"
    hero-subtitle="Sau khi xác thực thành công, hệ thống sẽ giúp bạn chọn quyền truy cập phù hợp với công việc bạn đang thực hiện."
    hero-badge="Clinic Queue"
    :features="[
      'Bảo mật dữ liệu',
      'Phân quyền theo chức năng (RBAC)',
      'Hỗ trợ nhiều quy trình khám bệnh',
    ]"
  >
    <!-- Slot Alert thông báo lỗi hoặc thông báo đăng ký thành công -->
    <template #alerts>
      <div v-if="route.query.registered === 'true'" class="mb-4">
        <BaseAlert
          type="success"
          title="Đăng ký thành công!"
          message="Tài khoản của bạn đã được tạo thành công. Vui lòng đăng nhập để bắt đầu sử dụng."
          dismissible
        />
      </div>
      <div v-else-if="authStore.error" class="mb-4">
        <BaseAlert type="error" :message="authStore.error" />
      </div>
    </template>

    <!-- Form Đăng nhập -->
    <form @submit.prevent="handleLogin" class="mb-6 space-y-4" novalidate>
      <BaseInput
        v-model="username"
        label="Tên đăng nhập"
        required
        placeholder="name"
        :error="errors.username"
        autocomplete="username"
      />

      <BasePasswordInput
        v-model="password"
        label="Mật khẩu"
        required
        placeholder="••••••••"
        :error="errors.password"
        autocomplete="current-password"
      />

      <!-- Ghi nhớ đăng nhập & Quên mật khẩu -->
      <div class="flex items-center justify-between pt-1">
        <BaseCheckbox
          v-model="rememberMe"
          label="Ghi nhớ đăng nhập"
          description="Lưu phiên đăng nhập trên thiết bị này"
        />
        <button
          type="button"
          @click="goToForgotPassword"
          class="ml-2 shrink-0 text-sm font-medium text-[#0E4D92] hover:underline focus:outline-none"
        >
          Quên mật khẩu?
        </button>
      </div>

      <!-- Nút Đăng nhập -->
      <div class="pt-2">
        <BaseButton
          type="submit"
          variant="primary"
          size="lg"
          block
          :loading="authStore.status === 'loading'"
          loading-text="Đang xử lý…"
        >
          Đăng nhập
        </BaseButton>
      </div>
    </form>

    <!-- Footer chuyển sang trang Đăng ký -->
    <template #footer>
      <p class="text-center text-sm text-slate-500">
        Chưa có tài khoản?
        <router-link to="/register" class="font-semibold text-[#0E4D92] hover:underline">
          Đăng ký ngay
        </router-link>
      </p>
    </template>
  </AuthLayout>
</template>
