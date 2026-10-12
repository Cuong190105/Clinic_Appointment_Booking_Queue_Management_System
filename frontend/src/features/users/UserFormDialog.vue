<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { CreateUserInput, UpdateUserInput, User } from './users.types'
import { validators } from '@/utils/validators'
import BaseModal from '@/components/ui/BaseModal.vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BaseSelect from '@/components/ui/BaseSelect.vue'
import BasePasswordInput from '@/components/ui/BasePasswordInput.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

const props = defineProps<{
  open: boolean
  initial?: User | null
  submitting?: boolean
}>()

const emit = defineEmits<{
  close: []
  submit: [payload: CreateUserInput | UpdateUserInput]
}>()

const form = reactive({
  username: '',
  password: '',
  fullName: '',
  email: '',
  phoneNumber: '',
  role: 'Receptionist',
  isActive: true,
})

const fieldErrors = reactive<Record<string, string>>({})

watch(
  () => [props.open, props.initial],
  () => {
    Object.keys(fieldErrors).forEach((k) => (fieldErrors[k] = ''))

    if (props.initial) {
      form.username = props.initial.username || ''
      form.password = ''
      form.fullName = props.initial.fullName || props.initial.name || ''
      form.email = props.initial.email || ''
      form.phoneNumber = props.initial.phoneNumber || ''
      // Ưu tiên chọn vai trò nhân viên nếu tài khoản có nhiều vai trò
      const staffRole = props.initial.roles?.find((r) => r === 'Admin' || r === 'Receptionist')
      form.role = staffRole || 'Receptionist'
      form.isActive = props.initial.isActive ?? true
    } else {
      form.username = ''
      form.password = 'User123456!'
      form.fullName = ''
      form.email = ''
      form.phoneNumber = ''
      form.role = 'Receptionist'
      form.isActive = true
    }
  },
  { immediate: true },
)

function validate(): boolean {
  Object.keys(fieldErrors).forEach((k) => (fieldErrors[k] = ''))

  const nameReq = validators.required(form.fullName, 'Họ và tên')
  if (!nameReq.isValid) fieldErrors.fullName = nameReq.message

  if (form.email) {
    const emailReq = validators.email(form.email)
    if (!emailReq.isValid) fieldErrors.email = emailReq.message
  }

  if (form.phoneNumber) {
    const phoneReq = validators.phone(form.phoneNumber)
    if (!phoneReq.isValid) fieldErrors.phoneNumber = phoneReq.message
  } else if (!props.initial) {
    fieldErrors.phoneNumber = 'Số điện thoại không được để trống'
  }

  if (!props.initial) {
    const userReq = validators.required(form.username, 'Tên đăng nhập')
    if (!userReq.isValid) fieldErrors.username = userReq.message
    else if (form.username.length < 6) fieldErrors.username = 'Tên đăng nhập tối thiểu 6 ký tự'

    const passReq = validators.password(form.password)
    if (!passReq.isValid) fieldErrors.password = passReq.message
  }

  return Object.values(fieldErrors).every((e) => !e)
}

function handleSubmit() {
  if (!validate()) return

  if (props.initial) {
    const payload: UpdateUserInput = {
      fullName: form.fullName.trim(),
      email: form.email.trim() || undefined,
      phoneNumber: form.phoneNumber.trim() || undefined,
      role: form.role,
      isActive: form.isActive,
    }
    emit('submit', payload)
  } else {
    const payload: CreateUserInput = {
      username: form.username.trim(),
      password: form.password,
      fullName: form.fullName.trim(),
      email: form.email.trim() || undefined,
      phoneNumber: form.phoneNumber.trim(),
      role: form.role,
    }
    emit('submit', payload)
  }
}
</script>

<template>
  <BaseModal
    :open="open"
    :title="initial ? 'Chỉnh sửa tài khoản' : 'Thêm tài khoản nhân viên'"
    :subtitle="initial ? 'Cập nhật quyền hạn và thông tin người dùng' : 'Tạo tài khoản quản trị viên hoặc lễ tân phòng khám'"
    size="md"
    icon="👤"
    @close="emit('close')"
  >
    <form @submit.prevent="handleSubmit" class="space-y-4 font-sans" novalidate>
      <!-- Họ và tên -->
      <BaseInput
        v-model="form.fullName"
        label="Họ và tên"
        required
        placeholder="Nguyễn Văn A"
        :error="fieldErrors.fullName"
      />

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <!-- Vai trò (Role) - Chỉ cho phép Lễ tân & Admin -->
        <div>
          <BaseSelect
            v-model="form.role"
            label="Vai trò hệ thống"
            required
            :options="[
              { value: 'Receptionist', label: 'Lễ tân (Receptionist)' },
              { value: 'Admin', label: 'Quản trị viên (Admin)' },
            ]"
          />
          <p class="text-[11px] text-slate-400 mt-1">
            * Tài khoản Bác sĩ được tạo tại mục <strong class="text-[#0E4D92]">Quản lý Bác sĩ</strong>.
          </p>
        </div>

        <!-- Số điện thoại -->
        <BaseInput
          v-model="form.phoneNumber"
          label="Số điện thoại"
          required
          placeholder="0912345678"
          :error="fieldErrors.phoneNumber"
        />
      </div>

      <!-- Email -->
      <BaseInput
        v-model="form.email"
        label="Email"
        type="email"
        placeholder="user@clinic.vn"
        :error="fieldErrors.email"
      />

      <!-- Tài khoản & Mật khẩu (chỉ khi tạo mới) -->
      <div v-if="!initial" class="space-y-4 p-4 rounded-xl bg-slate-50 border border-slate-200">
        <BaseInput
          v-model="form.username"
          label="Tên đăng nhập"
          required
          placeholder="username (tối thiểu 6 ký tự)"
          :error="fieldErrors.username"
        />

        <BasePasswordInput
          v-model="form.password"
          label="Mật khẩu khởi tạo"
          required
          placeholder="Tối thiểu 6 ký tự (gồm hoa, thường, số)"
          :error="fieldErrors.password"
        />
      </div>

      <!-- Trạng thái hoạt động (khi sửa) -->
      <div v-if="initial" class="flex items-center justify-between p-3.5 rounded-xl bg-slate-50 border border-slate-200">
        <div>
          <p class="text-sm font-medium text-slate-800">Trạng thái tài khoản</p>
          <p class="text-xs text-slate-500">Cho phép hoặc khóa đăng nhập vào hệ thống</p>
        </div>
        <label class="relative inline-flex items-center cursor-pointer">
          <input type="checkbox" v-model="form.isActive" class="sr-only peer" />
          <div class="w-11 h-6 bg-slate-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-slate-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-[#0E4D92]"></div>
        </label>
      </div>

      <div class="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
        <BaseButton
          type="button"
          variant="outline"
          size="md"
          @click="emit('close')"
          :disabled="submitting"
        >
          Hủy bỏ
        </BaseButton>

        <BaseButton
          type="submit"
          variant="primary"
          size="md"
          :loading="submitting"
          :loading-text="initial ? 'Đang lưu…' : 'Đang tạo…'"
        >
          {{ initial ? 'Lưu thay đổi' : 'Thêm tài khoản' }}
        </BaseButton>
      </div>
    </form>
  </BaseModal>
</template>
