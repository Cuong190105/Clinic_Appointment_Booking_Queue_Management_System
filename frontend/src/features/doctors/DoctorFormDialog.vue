<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { specialtiesApi } from '@/features/specialties/specialties.api'
import type { Specialty } from '@/features/specialties/specialties.types'
import type { CreateDoctorPayload, Doctor, UpdateDoctorPayload } from './doctors.types'
import { validators } from '@/utils/validators'
import BaseModal from '@/components/ui/BaseModal.vue'
import BaseAlert from '@/components/ui/BaseAlert.vue'
import BaseInput from '@/components/ui/BaseInput.vue'
import BaseSelect from '@/components/ui/BaseSelect.vue'
import BaseTextarea from '@/components/ui/BaseTextarea.vue'
import BasePasswordInput from '@/components/ui/BasePasswordInput.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

const props = defineProps<{
  open: boolean
  doctor?: Doctor | null
  submitting?: boolean
}>()

const emit = defineEmits<{
  close: []
  submit: [payload: CreateDoctorPayload | UpdateDoctorPayload]
}>()

const specialties = ref<Specialty[]>([])
const loadingSpecialties = ref(false)
const error = ref('')

const form = reactive({
  username: '',
  password: '',
  fullName: '',
  email: '',
  phoneNumber: '',
  dateOfBirth: '',
  gender: 0,
  address: '',
  specialtyId: '',
  licenseNumber: '',
  qualification: '',
  experienceYears: 1,
  biography: '',
})

const fieldErrors = reactive<Record<string, string>>({})

watch(
  () => props.open,
  async (isOpen) => {
    if (isOpen) {
      error.value = ''
      Object.keys(fieldErrors).forEach((k) => (fieldErrors[k] = ''))

      // Load specialties dropdown
      if (specialties.value.length === 0) {
        loadingSpecialties.value = true
        try {
          const res = await specialtiesApi.list({ pageSize: 100 })
          specialties.value = res.items
        } catch {
          // ignore
        } finally {
          loadingSpecialties.value = false
        }
      }

      if (props.doctor) {
        // Edit mode
        form.username = props.doctor.email?.split('@')[0] || ''
        form.password = ''
        form.fullName = props.doctor.fullName || props.doctor.name || ''
        form.email = props.doctor.email || ''
        form.phoneNumber = props.doctor.phoneNumber || ''
        form.dateOfBirth = props.doctor.dateOfBirth || '1985-01-01'
        form.gender = typeof props.doctor.gender === 'string'
          ? (props.doctor.gender.toLowerCase() === 'female' ? 1 : 0)
          : (props.doctor.gender ?? 0)
        form.address = props.doctor.address || 'Hà Nội'
        form.specialtyId = props.doctor.specialtyId || specialties.value[0]?.id || ''
        form.licenseNumber = props.doctor.licenseNumber || ''
        form.qualification = props.doctor.qualification || 'Bác sĩ chuyên khoa'
        form.experienceYears = props.doctor.experienceYears || 1
        form.biography = props.doctor.biography || ''
      } else {
        // Create mode
        form.username = ''
        form.password = 'Doctor123!'
        form.fullName = ''
        form.email = ''
        form.phoneNumber = ''
        form.dateOfBirth = '1990-01-01'
        form.gender = 0
        form.address = 'Hà Nội'
        form.specialtyId = specialties.value[0]?.id || ''
        form.licenseNumber = ''
        form.qualification = 'Bác sĩ chuyên khoa I'
        form.experienceYears = 3
        form.biography = ''
      }
    }
  },
)

function validate(): boolean {
  Object.keys(fieldErrors).forEach((k) => (fieldErrors[k] = ''))

  const nameReq = validators.required(form.fullName, 'Họ và tên')
  if (!nameReq.isValid) fieldErrors.fullName = nameReq.message
  else if (form.fullName.length < 6) fieldErrors.fullName = 'Họ và tên tối thiểu 6 ký tự'

  const emailReq = validators.email(form.email)
  if (!emailReq.isValid) fieldErrors.email = emailReq.message

  const phoneReq = validators.phone(form.phoneNumber)
  if (!phoneReq.isValid) fieldErrors.phoneNumber = phoneReq.message

  const licenseReq = validators.required(form.licenseNumber, 'Số CCHN')
  if (!licenseReq.isValid) fieldErrors.licenseNumber = licenseReq.message

  if (!props.doctor) {
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

  if (props.doctor) {
    const payload: UpdateDoctorPayload = {
      fullName: form.fullName.trim(),
      email: form.email.trim(),
      phoneNumber: form.phoneNumber.trim(),
      dateOfBirth: form.dateOfBirth,
      gender: Number(form.gender),
      address: form.address.trim() || 'Hà Nội',
      licenseNumber: form.licenseNumber.trim(),
      qualification: form.qualification.trim() || 'Bác sĩ',
      experienceYears: Number(form.experienceYears) || 1,
      specialtyId: form.specialtyId || undefined,
      biography: form.biography.trim() || null,
    }
    emit('submit', payload)
  } else {
    const payload: CreateDoctorPayload = {
      username: form.username.trim(),
      password: form.password,
      fullName: form.fullName.trim(),
      email: form.email.trim(),
      phoneNumber: form.phoneNumber.trim(),
      dateOfBirth: form.dateOfBirth,
      gender: Number(form.gender),
      address: form.address.trim() || 'Hà Nội',
      licenseNumber: form.licenseNumber.trim(),
      qualification: form.qualification.trim() || 'Bác sĩ chuyên khoa I',
      experienceYears: Number(form.experienceYears) || 1,
      specialtyId: form.specialtyId,
      biography: form.biography.trim() || null,
    }
    emit('submit', payload)
  }
}
</script>

<template>
  <BaseModal
    :open="open"
    :title="doctor ? 'Chỉnh sửa thông tin Bác sĩ' : 'Thêm Bác sĩ mới'"
    :subtitle="doctor ? 'Cập nhật hồ sơ chuyên môn và thông tin liên hệ' : 'Tạo tài khoản và hồ sơ bác sĩ trên hệ thống'"
    size="2xl"
    icon="🩺"
    @close="emit('close')"
  >
    <div v-if="error" class="mb-4">
      <BaseAlert type="error" :message="error" />
    </div>

    <form id="doctor-form" @submit.prevent="handleSubmit" class="space-y-4 font-sans" novalidate>
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <!-- Họ và tên -->
        <BaseInput
          v-model="form.fullName"
          label="Họ và tên"
          required
          placeholder="BS. Nguyễn Văn A (tối thiểu 6 ký tự)"
          :error="fieldErrors.fullName"
        />

        <!-- Chuyên khoa -->
        <BaseSelect
          v-model="form.specialtyId"
          label="Chuyên khoa"
          required
          :options="specialties.map((s) => ({ value: s.id, label: s.name }))"
        />
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <!-- Email -->
        <BaseInput
          v-model="form.email"
          label="Email liên hệ"
          type="email"
          required
          placeholder="doctor@clinic.vn"
          :error="fieldErrors.email"
        />

        <!-- Số điện thoại -->
        <BaseInput
          v-model="form.phoneNumber"
          label="Số điện thoại"
          required
          placeholder="0912345678"
          :error="fieldErrors.phoneNumber"
        />
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <!-- Số chứng chỉ hành nghề CCHN -->
        <BaseInput
          v-model="form.licenseNumber"
          label="Số chứng chỉ hành nghề (CCHN)"
          required
          placeholder="CCHN-12345/BYT"
          :error="fieldErrors.licenseNumber"
        />

        <!-- Trình độ chuyên môn -->
        <BaseInput
          v-model="form.qualification"
          label="Trình độ chuyên môn"
          placeholder="Bác sĩ chuyên khoa I, Thạc sĩ..."
        />
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <!-- Số năm kinh nghiệm -->
        <BaseInput
          v-model="form.experienceYears"
          label="Kinh nghiệm (năm)"
          type="number"
          min="0"
        />

        <!-- Ngày sinh -->
        <BaseInput
          v-model="form.dateOfBirth"
          label="Ngày sinh"
          type="date"
          required
        />

        <!-- Giới tính -->
        <BaseSelect
          v-model="form.gender"
          label="Giới tính"
          :options="[
            { value: 0, label: 'Nam' },
            { value: 1, label: 'Nữ' },
            { value: 2, label: 'Khác' },
          ]"
        />
      </div>

      <!-- Tài khoản & Mật khẩu (chỉ hiển thị khi tạo mới) -->
      <div v-if="!doctor" class="grid grid-cols-1 sm:grid-cols-2 gap-4 p-4 rounded-xl bg-slate-50 border border-slate-200">
        <BaseInput
          v-model="form.username"
          label="Tên đăng nhập"
          required
          placeholder="bacsi.a (tối thiểu 6 ký tự)"
          :error="fieldErrors.username"
        />

        <BasePasswordInput
          v-model="form.password"
          label="Mật khẩu khởi tạo"
          required
          placeholder="Tối thiểu 6 ký tự (hoa, thường, số)"
          :error="fieldErrors.password"
        />
      </div>

      <!-- Địa chỉ -->
      <BaseInput
        v-model="form.address"
        label="Địa chỉ"
        placeholder="Số nhà, đường, quận/huyện, tỉnh/thành phố"
      />

      <!-- Tiểu sử -->
      <BaseTextarea
        v-model="form.biography"
        label="Tiểu sử / Giới thiệu chuyên môn"
        placeholder="Kinh nghiệm công tác, quá trình đào tạo, lĩnh vực chuyên sâu..."
        :rows="3"
      />
    </form>

    <template #footer>
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
        form="doctor-form"
        variant="primary"
        size="md"
        :loading="submitting"
        :loading-text="doctor ? 'Đang lưu…' : 'Đang tạo…'"
        @click="handleSubmit"
      >
        {{ doctor ? 'Lưu thay đổi' : 'Thêm Bác sĩ' }}
      </BaseButton>
    </template>
  </BaseModal>
</template>
