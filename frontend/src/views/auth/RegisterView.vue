<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { validators } from '@/utils/validators'
import { useFormValidation } from '@/composables/useFormValidation'
import AuthLayout from '@/components/layout/AuthLayout.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BaseSelect from '@/components/ui/BaseSelect.vue'
import BaseTextarea from '@/components/ui/BaseTextarea.vue'
import BasePasswordInput from '@/components/ui/BasePasswordInput.vue'
import BaseCheckbox from '@/components/ui/BaseCheckbox.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

// Khởi tạo router và authStore để thao tác với luồng người dùng
const router = useRouter()
const authStore = useAuthStore()

// Trích xuất thông báo lỗi từ authStore để hiển thị lỗi backend trong form validation
const { error: backendError } = storeToRefs(authStore)

// Khởi tạo các biến và hàm xử lý form từ composable
const {
  formData,
  errors,
  touched,
  isSubmitting,
  handleBlur,
  validateAll,
} = useFormValidation(
  {
    username: '',
    fullName: '',
    phoneNumber: '',
    email: '',
    password: '',
    confirmPassword: '',
    gender: 'Male' as 'Male' | 'Female' | 'Other',
    dateOfBirth: '',
    address: '',
    rememberMe: false,
  },

  // Các quy tắc kiểm tra (validation rules) cho từng trường dữ liệu
  {
    username: [
      (val) => validators.required(val, 'Tên đăng nhập'),
      (val) =>
        val && val.trim().length < 8
          ? { isValid: false, message: 'Tên đăng nhập phải có ít nhất 8 ký tự' }
          : { isValid: true, message: '' },
    ],
    fullName: [(val) => validators.required(val, 'Họ và tên')],
    phoneNumber: [
      (val) => validators.required(val, 'Số điện thoại'),
      (val) => validators.phone(val),
    ],
    email: [
      (val) => validators.required(val, 'Email'),
      (val) => validators.email(val),
    ],
    address: [(val) => validators.required(val, 'Địa chỉ')],
    dateOfBirth: [
      (val) => (val ? { isValid: true, message: '' } : { isValid: false, message: 'Ngày sinh là bắt buộc' }),
    ],
    password: [
      (val) => validators.required(val, 'Mật khẩu'),
      (val) =>
        val && val.trim().length < 8
          ? { isValid: false, message: 'Mật khẩu phải có ít nhất 8 ký tự' }
          : { isValid: true, message: '' },
      (val) => validators.password(val, 8),
    ],
    confirmPassword: [(val, formData) => validators.confirmPassword(formData.password, val)],
  },
  // truyền lỗi từ be vào composable để hiển thị giao diện
  { externalError: backendError },
)

const genderOptions = [
  { label: 'Nam', value: 'Male' },
  { label: 'Nữ', value: 'Female' },
  { label: 'Khác', value: 'Other' },
]

// Hàm xử lý khi người dùng nhấn nút đăng ký
async function handleRegister() {
  if (!validateAll()) return

  isSubmitting.value = true
  try {
    await authStore.register({
      ...formData,
      username: (formData.username || formData.email).trim(),
      email: formData.email.trim(),
      fullName: formData.fullName.trim(),
      phoneNumber: formData.phoneNumber.trim(),
      address: formData.address?.trim() || 'Chưa cập nhật',
      dateOfBirth: formData.dateOfBirth,
      gender: formData.gender,
    })

    if (authStore.isAuthenticated) {
      router.push('/select-role')
    } else {
      router.push({ path: '/login', query: { registered: 'true' } })
    }
  } catch {
    /* Lỗi đã được lưu trong authStore */
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <AuthLayout
    title="Đăng ký tài khoản"
    subtitle="Nhập thông tin cá nhân để tạo hồ sơ khám bệnh."
    hero-title="Tạo tài khoản để&#10;truy cập hệ thống"
    hero-subtitle="Đăng ký tài khoản để tiếp tục sử dụng hệ thống quản lý khám bệnh, lịch hẹn và quy trình nội bộ theo vai trò được phân quyền."
    hero-badge="Hệ thống phòng khám"
    :features="[
      'Tạo hồ sơ bệnh nhân trực tuyến',
      'Đặt lịch hẹn khám nhanh chóng',
      'Theo dõi tiến trình và số thứ tự khám thời gian thực',
    ]"
  >
    <!-- Slot Alert hiển thị lỗi -->
    <template #alerts>
      <div v-if="authStore.error" class="mb-4">
        <BaseAlert type="error" :message="authStore.error" />
      </div>
    </template>

    <!-- Form Đăng ký -->
    <form @submit.prevent="handleRegister" class="space-y-4 mb-6" novalidate>
      <!-- Tên đăng nhập -->
      <BaseInput
        v-model="formData.username"
        label="Tên đăng nhập"
        required
        placeholder="patient01"
        :error="touched.username && errors.username ? errors.username : ''"
        @blur="handleBlur('username')"
        autocomplete="username"
      />

      <!-- Họ và tên -->
      <BaseInput
        v-model="formData.fullName"
        label="Họ và tên"
        required
        placeholder="Nguyễn Văn A"
        :error="touched.fullName && errors.fullName ? errors.fullName : ''"
        @blur="handleBlur('fullName')"
        autocomplete="name"
      />

      <!-- SĐT & Giới tính -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <BaseInput
          v-model="formData.phoneNumber"
          label="Số điện thoại"
          required
          type="tel"
          placeholder="0912345678"
          :error="touched.phoneNumber && errors.phoneNumber ? errors.phoneNumber : ''"
          @blur="handleBlur('phoneNumber')"
          autocomplete="tel"
        />

        <BaseSelect
          v-model="formData.gender"
          label="Giới tính"
          :options="genderOptions"
        />
      </div>

      <!-- Ngày sinh & Email -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <BaseInput
          v-model="formData.dateOfBirth"
          label="Ngày sinh"
          type="date"
          :error="touched.dateOfBirth && errors.dateOfBirth ? errors.dateOfBirth : ''"
          @blur="handleBlur('dateOfBirth')"
        />

        <BaseInput
          v-model="formData.email"
          label="Email"
          required
          type="email"
          placeholder="email@example.com"
          :error="touched.email && errors.email ? errors.email : ''"
          @blur="handleBlur('email')"
          autocomplete="email"
        />
      </div>

      <!-- Địa chỉ -->
      <BaseTextarea
        v-model="formData.address"
        label="Địa chỉ"
        required
        :rows="2"
        placeholder="Số nhà, đường, phường, quận..."
        :error="touched.address && errors.address ? errors.address : ''"
        @blur="handleBlur('address')"
      />

      <!-- Mật khẩu & Xác nhận mật khẩu -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <BasePasswordInput
          v-model="formData.password"
          label="Mật khẩu"
          required
          placeholder="••••••••"
          :error="touched.password && errors.password ? errors.password : ''"
          @blur="handleBlur('password')"
          autocomplete="new-password"
        />

        <BasePasswordInput
          v-model="formData.confirmPassword"
          label="Xác nhận mật khẩu"
          required
          placeholder="••••••••"
          :error="touched.confirmPassword && errors.confirmPassword ? errors.confirmPassword : ''"
          @blur="handleBlur('confirmPassword')"
          autocomplete="new-password"
        />
      </div>

      <!-- Ghi nhớ đăng nhập -->
      <div class="pt-1">
        <BaseCheckbox
          v-model="formData.rememberMe"
          label="Ghi nhớ đăng nhập sau khi đăng ký"
          description="Nếu bỏ tích, hệ thống chỉ giữ phiên đăng nhập trong trình duyệt hiện tại."
        />
      </div>

      <!-- Nút Đăng ký -->
      <div class="pt-2">
        <BaseButton
          type="submit"
          variant="primary"
          size="lg"
          block
          :loading="isSubmitting || authStore.status === 'loading'"
          loading-text="Đang tạo tài khoản..."
        >
          Đăng ký ngay
        </BaseButton>
      </div>
    </form>

    <!-- Footer chuyển sang Đăng nhập -->
    <template #footer>
      <p class="text-center text-sm text-slate-500">
        Đã có tài khoản?
        <router-link to="/login" class="text-[#0E4D92] font-semibold hover:underline">
          Đăng nhập
        </router-link>
      </p>
    </template>
  </AuthLayout>
</template>